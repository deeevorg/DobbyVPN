using System.Diagnostics;
using System.Text.Json;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using RismanNG.Application;
using RismanNG.Domain;
namespace RismanNG.Infrastructure;
public sealed class SingBoxConfigFactory : ISingBoxConfigFactory
{
    public string Create(Profile profile, int localPort, int apiPort)
    {
        if (profile.Protocol == Protocol.SingBoxJson) return profile.RawConfig ?? throw new InvalidOperationException("Invalid sing-box configuration.");
        var outbound = new Dictionary<string, object?> { ["type"] = profile.Protocol.ToString().ToLowerInvariant().Replace("hysteria2", "hysteria2"), ["tag"] = "proxy", ["server"] = profile.Host, ["server_port"] = profile.Port };
        switch (profile.Protocol) { case Protocol.Vless: outbound["uuid"] = profile.Secret; break; case Protocol.Vmess: outbound["uuid"] = profile.Secret; break; case Protocol.Trojan: outbound["password"] = profile.Secret; break; case Protocol.Shadowsocks: outbound["method"] = profile.Secret.Split(':', 2)[0]; outbound["password"] = profile.Secret.Split(':', 2).Last(); break; case Protocol.Hysteria2: outbound["password"] = profile.Secret; break; }
        if (profile.Protocol == Protocol.Vless && profile.Options.TryGetValue("encryption", out var encryption) && encryption != "none") throw new InvalidOperationException("Only VLESS encryption=none is supported.");
        if (profile.Options.TryGetValue("flow", out var flow) && !string.IsNullOrWhiteSpace(flow)) outbound["flow"] = flow;
        if (profile.Options.TryGetValue("security", out var sec) && sec is "tls" or "reality")
        {
            var tls = new Dictionary<string, object?> { ["enabled"] = true, ["server_name"] = profile.Options.TryGetValue("sni", out var sni) ? sni : profile.Host };
            if (profile.Options.TryGetValue("alpn", out var alpn) && !string.IsNullOrWhiteSpace(alpn)) tls["alpn"] = alpn.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if ((profile.Options.TryGetValue("allowInsecure", out var allowInsecure) && allowInsecure is "1" or "true") || (profile.Options.TryGetValue("insecure", out var insecure) && insecure is "1" or "true")) tls["insecure"] = true;
            if (profile.Options.TryGetValue("fp", out var fingerprint) && !string.IsNullOrWhiteSpace(fingerprint)) tls["utls"] = new Dictionary<string, object?> { ["enabled"] = true, ["fingerprint"] = fingerprint };
            if (sec == "reality") tls["reality"] = new Dictionary<string, object?> { ["enabled"] = true, ["public_key"] = profile.Options["pbk"], ["short_id"] = profile.Options.TryGetValue("sid", out var sid) ? sid : "" };
            outbound["tls"] = tls;
        }
        if (profile.Options.TryGetValue("type", out var transport) && transport == "ws") outbound["transport"] = new Dictionary<string, object?> { ["type"] = "ws", ["path"] = profile.Options.TryGetValue("path", out var path) ? path : "/", ["headers"] = profile.Options.TryGetValue("host", out var host) ? new Dictionary<string, string> { ["Host"] = host } : null };
        if (profile.Options.TryGetValue("type", out transport) && transport == "grpc") outbound["transport"] = new Dictionary<string, object?> { ["type"] = "grpc", ["service_name"] = profile.Options.TryGetValue("serviceName", out var service) ? service : "" };
        return JsonSerializer.Serialize(new { log = new { level = "warn", timestamp = true }, inbounds = new[] { new { type = "mixed", tag = "mixed-in", listen = "127.0.0.1", listen_port = localPort } }, outbounds = new object[] { outbound, new { type = "direct", tag = "direct" } }, route = new { final_ = "proxy" }, experimental = new { clash_api = new { external_controller = $"127.0.0.1:{apiPort}" } } }).Replace("\"final_\"", "\"final\"");
    }
}
public sealed class ConnectionService(IProfileRepository profiles, ISingBoxConfigFactory singBoxFactory, IXrayConfigFactory xrayFactory, AppSettings settings, WindowsProxyManager proxy) : IConnectionService
{
    private Process? _process; private string? _tempPath;
    private CancellationTokenSource? _metricsCancellation; private int _localPort; private int _apiPort; private string? _lastCoreError;
    private bool _usingXray;
    public event EventHandler<ConnectionMetrics>? MetricsUpdated;
    public string ActiveCore { get; private set; } = "None";
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_process is { HasExited: false }) throw new InvalidOperationException("A connection is already active.");
        _lastCoreError = null;
        var profile = (await profiles.GetAllAsync(ct)).FirstOrDefault(x => x.IsActive) ?? throw new InvalidOperationException("Select a profile before connecting.");
        _usingXray = UsesXray(profile);
        ActiveCore = _usingXray ? "Xray-core" : "sing-box";
        var exe = ResolveCorePath(_usingXray);
        if (!File.Exists(exe)) throw new InvalidOperationException($"The bundled {ActiveCore} core is missing. Reinstall this application.");
        _localPort = GetFreePort(); _apiPort = GetFreePort();
        _tempPath = Path.Combine(Path.GetTempPath(), $"rismanng-{Guid.NewGuid():N}.json");
        var config = _usingXray ? xrayFactory.Create(profile, _localPort, _apiPort) : singBoxFactory.Create(profile, _localPort, _apiPort);
        await File.WriteAllTextAsync(_tempPath, config, ct);
        var info = new ProcessStartInfo(exe) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
        info.ArgumentList.Add("run"); info.ArgumentList.Add("-c"); info.ArgumentList.Add(_tempPath);
        _process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {ActiveCore}.");
        AppLog.Write("connection", $"{ActiveCore} started for {profile.Protocol}; process={_process.Id}; local_port={_localPort}");
        _ = CaptureOutputAsync(_process.StandardOutput, $"{ActiveCore}.stdout");
        _ = CaptureOutputAsync(_process.StandardError, $"{ActiveCore}.stderr", ObserveCoreError);
        _ = ObserveExitAsync(_process, ActiveCore);
        await Task.Delay(900, ct);
        if (_process.HasExited) throw new InvalidOperationException($"{ActiveCore} rejected the generated configuration. See local Diagnostics.");

        // A listening local proxy is not evidence that the remote server accepted the profile.
        var exitIp = await ProbeProxyRouteAsync(ct);
        if (_process.HasExited) throw new InvalidOperationException($"{ActiveCore} stopped during route verification.");
        await proxy.ApplyAsync($"127.0.0.1:{_localPort}", ct);
        _metricsCancellation = new CancellationTokenSource();
        MetricsUpdated?.Invoke(this, new ConnectionMetrics(0, 0, ExitIp: exitIp));
        if (_usingXray) _ = MonitorXrayTrafficAsync(exe, _metricsCancellation.Token);
        else _ = MonitorTrafficAsync(_metricsCancellation.Token);
        _ = MeasureServerLatencyAsync(profile, _metricsCancellation.Token);
        _ = MonitorRouteAsync(_metricsCancellation.Token);
    }
    private static bool UsesXray(Profile profile) => profile.Protocol is not (Protocol.SingBoxJson or Protocol.Hysteria2);
    private string ResolveCorePath(bool xray) => xray ? Path.Combine(AppContext.BaseDirectory, "Core", "Xray", "xray.exe") : File.Exists(Path.Combine(AppContext.BaseDirectory, "Core", "sing-box.exe")) ? Path.Combine(AppContext.BaseDirectory, "Core", "sing-box.exe") : settings.SingBoxPath ?? "";
    private async Task MonitorTrafficAsync(CancellationToken ct) { try { using var socket = new ClientWebSocket(); await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{_apiPort}/traffic"), ct); AppLog.Write("metrics", "Connected to sing-box traffic API."); var buffer = new byte[4096]; while (!ct.IsCancellationRequested) { var result = await socket.ReceiveAsync(buffer, ct); if (result.MessageType != WebSocketMessageType.Text) continue; using var json = JsonDocument.Parse(buffer.AsMemory(0, result.Count)); var root = json.RootElement; var up = root.TryGetProperty("up", out var upValue) ? upValue.GetInt64() : 0; var down = root.TryGetProperty("down", out var downValue) ? downValue.GetInt64() : 0; MetricsUpdated?.Invoke(this, new ConnectionMetrics(up, down)); } } catch (OperationCanceledException) { } catch (Exception ex) { AppLog.Write("metrics", $"Traffic API failure: {ex.GetType().Name}: {ex.Message}"); MetricsUpdated?.Invoke(this, new ConnectionMetrics(0, 0, Error: "Traffic statistics are unavailable from sing-box.")); } }
    private async Task MonitorXrayTrafficAsync(string xrayExe, CancellationToken ct) { try { while (!ct.IsCancellationRequested) { var up = await QueryXrayStatAsync(xrayExe, "outbound>>>proxy>>>traffic>>>uplink", ct); var down = await QueryXrayStatAsync(xrayExe, "outbound>>>proxy>>>traffic>>>downlink", ct); MetricsUpdated?.Invoke(this, new ConnectionMetrics(up, down)); await Task.Delay(TimeSpan.FromSeconds(2), ct); } } catch (OperationCanceledException) { } catch (Exception ex) { AppLog.Write("metrics", $"Xray statistics failure: {ex.GetType().Name}: {ex.Message}"); MetricsUpdated?.Invoke(this, new ConnectionMetrics(0, 0, Error: "Traffic statistics are unavailable from Xray-core.")); } }
    private async Task<long> QueryXrayStatAsync(string xrayExe, string pattern, CancellationToken ct) { var start = new ProcessStartInfo(xrayExe) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }; start.ArgumentList.Add("api"); start.ArgumentList.Add("statsquery"); start.ArgumentList.Add("--server"); start.ArgumentList.Add($"127.0.0.1:{_apiPort}"); start.ArgumentList.Add("--pattern"); start.ArgumentList.Add(pattern); using var query = Process.Start(start) ?? throw new InvalidOperationException("Could not query Xray statistics."); var output = await query.StandardOutput.ReadToEndAsync(ct); await query.WaitForExitAsync(ct); if (query.ExitCode != 0) { var error = await query.StandardError.ReadToEndAsync(ct); throw new InvalidOperationException(error.Trim()); } var values = Regex.Matches(output, "\\\"?value\\\"?\\s*:\\s*\\\"?(\\d+)").Select(x => long.Parse(x.Groups[1].Value)).ToArray(); return values.Sum(); }
    private async Task MeasureServerLatencyAsync(Profile profile, CancellationToken ct) { try { using var client = new TcpClient(); var stopwatch = Stopwatch.StartNew(); await client.ConnectAsync(profile.Host, profile.Port, ct); stopwatch.Stop(); AppLog.Write("metrics", $"Server TCP connection succeeded; latency={stopwatch.ElapsedMilliseconds}ms."); MetricsUpdated?.Invoke(this, new ConnectionMetrics(0, 0, ServerLatency: stopwatch.Elapsed)); } catch (Exception ex) { AppLog.Write("metrics", $"Server TCP connection failed: {ex.GetType().Name}: {ex.Message}"); } }
    private async Task<string> ProbeProxyRouteAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(14));
        var stopwatch = Stopwatch.StartNew();
        var probes = new List<Task<string>>
        {
            ProbeEndpointAsync("https://api.ipify.org", timeout.Token),
            ProbeEndpointAsync("https://icanhazip.com", timeout.Token)
        };
        Exception? lastError = null;
        while (probes.Count > 0)
        {
            var completed = await Task.WhenAny(probes);
            probes.Remove(completed);
            try
            {
                var ip = await completed;
                timeout.Cancel();
                AppLog.Write("metrics", $"Verified proxy exit IP {ip}; duration={stopwatch.ElapsedMilliseconds}ms.");
                return ip;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { lastError = ex; }
        }
        AppLog.Write("metrics", $"Proxy route failed: {lastError?.GetType().Name}: {lastError?.Message}");
        throw new InvalidOperationException(_lastCoreError ?? "No internet route was verified through this profile. Check the network and profile configuration.", lastError);
    }

    private async Task<string> ProbeEndpointAsync(string endpoint, CancellationToken ct)
    {
        using var handler = new HttpClientHandler { Proxy = new WebProxy($"http://127.0.0.1:{_localPort}"), UseProxy = true, AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var response = await client.GetAsync($"{endpoint}/?nonce={Guid.NewGuid():N}", ct);
        response.EnsureSuccessStatusCode();
        var text = (await response.Content.ReadAsStringAsync(ct)).Trim();
        if (!IPAddress.TryParse(text, out var ip) || IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || IsPrivateIpv4(ip))
            throw new InvalidOperationException("The proxy did not return a public exit IP.");
        return ip.ToString();
    }

    private static bool IsPrivateIpv4(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var bytes = ip.GetAddressBytes();
        return bytes[0] is 0 or 10 or 127 || bytes[0] == 169 && bytes[1] == 254 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 192 && bytes[1] == 168;
    }

    private async Task MonitorRouteAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                if (_process is null || _process.HasExited) throw new InvalidOperationException($"{ActiveCore} stopped unexpectedly.");
                var ip = await ProbeProxyRouteAsync(ct);
                MetricsUpdated?.Invoke(this, new ConnectionMetrics(0, 0, ExitIp: ip));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppLog.Write("connection", $"Verified proxy route lost: {ex.GetType().Name}: {ex.Message}");
            MetricsUpdated?.Invoke(this, new ConnectionMetrics(0, 0, Error: ex.Message, RouteLost: true));
        }
    }
    private static int GetFreePort() { using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); return ((IPEndPoint)listener.LocalEndpoint).Port; }
    private void ObserveCoreError(string line) { if (line.Contains("reality verification failed", StringComparison.OrdinalIgnoreCase)) _lastCoreError = "Reality verification failed: the server's Reality parameters no longer match this profile. Import an updated link from the provider."; }
    private static async Task CaptureOutputAsync(StreamReader reader, string area, Action<string>? observer = null) { for (var count = 0; count < 100; count++) { var line = await reader.ReadLineAsync().ConfigureAwait(false); if (line is null) break; observer?.Invoke(line); AppLog.Write(area, line); } }
    private static async Task ObserveExitAsync(Process process, string core) { await process.WaitForExitAsync(); AppLog.Write("connection", $"{core} exited with code {process.ExitCode}."); }
    public async Task DisconnectAsync(CancellationToken ct = default) { _metricsCancellation?.Cancel(); _metricsCancellation?.Dispose(); _metricsCancellation = null; if (_process is { HasExited: false }) { _process.Kill(entireProcessTree: true); await _process.WaitForExitAsync(ct); } _process?.Dispose(); _process = null; ActiveCore = "None"; await proxy.RestoreAsync(ct); AppLog.Write("connection", "Disconnected and attempted proxy restore."); if (_tempPath is not null) { try { File.Delete(_tempPath); } catch { } _tempPath = null; } }
}
public static class SafeError { public static string ForUser(Exception ex) => ex is OperationCanceledException ? "Operation canceled." : ex is InvalidOperationException ? ex.Message : "The operation could not be completed. Check your input and Diagnostics."; }
