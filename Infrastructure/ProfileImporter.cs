using System.Text;
using System.Text.Json;
using RismanNG.Application;
using RismanNG.Domain;
namespace RismanNG.Infrastructure;
public sealed class ProfileImporter : IProfileImporter
{
    public IReadOnlyList<Profile> Parse(string input)
    {
        var (profiles, rejected) = ParseLines(input);
        if (rejected > 0) throw new InvalidOperationException("Some links were invalid or use unsupported transport options; no partial import was saved.");
        return profiles;
    }
    public (IReadOnlyList<Profile> Profiles, int Rejected) ParseSubscription(string input) => ParseLines(input);
    private static (IReadOnlyList<Profile> Profiles, int Rejected) ParseLines(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new InvalidOperationException("Nothing was provided to import.");
        input = TryDecodeSubscription(input.Trim());
        if (input.TrimStart().StartsWith('{')) return ([new(Guid.NewGuid(), "sing-box configuration", Protocol.SingBoxJson, "", 0, "", new Dictionary<string,string>(), RawConfig: input)], 0);
        var profiles = new List<Profile>(); var failures = 0;
        foreach (var line in input.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) try { profiles.Add(ParseUrl(line)); } catch { failures++; }
        if (profiles.Count == 0) throw new InvalidOperationException("No supported, valid profile links were found.");
        return (profiles, failures);
    }
    private static Profile ParseUrl(string text)
    {
        if (text.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase)) return ParseVmess(text);
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) throw new InvalidOperationException();
        var protocol = uri.Scheme.ToLowerInvariant() switch { "vless" => Protocol.Vless, "trojan" => Protocol.Trojan, "ss" => Protocol.Shadowsocks, "hy2" or "hysteria2" => Protocol.Hysteria2, _ => throw new InvalidOperationException() };
        if (string.IsNullOrWhiteSpace(uri.Host) || uri.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(uri.UserInfo)) throw new InvalidOperationException();
        var options = ParseQuery(uri.Query);
        ValidateOptions(protocol, options);
        if (protocol == Protocol.Vless && options.TryGetValue("type", out var transport) && transport is not ("tcp" or "ws" or "grpc")) throw new InvalidOperationException();
        if (protocol == Protocol.Vless && options.TryGetValue("security", out var security) && security is not ("tls" or "reality" or "none")) throw new InvalidOperationException("Unsupported VLESS security mode.");
        if (protocol == Protocol.Vless && options.TryGetValue("security", out security) && security == "reality" && (!options.ContainsKey("pbk") || !options.ContainsKey("sni"))) throw new InvalidOperationException("Reality links must include both pbk and sni.");
        return new(Guid.NewGuid(), Uri.UnescapeDataString(uri.Fragment.TrimStart('#')) is { Length: > 0 } name ? name : $"{protocol} {uri.Host}", protocol, uri.Host, uri.Port, Uri.UnescapeDataString(uri.UserInfo), options);
    }
    private static Profile ParseVmess(string text)
    { var b64 = text[8..].Trim(); var json = Encoding.UTF8.GetString(Convert.FromBase64String(Pad(b64))); using var doc = JsonDocument.Parse(json); var r = doc.RootElement; var host = r.GetProperty("add").GetString() ?? throw new InvalidOperationException(); var port = int.Parse(r.GetProperty("port").ToString()); var id = r.GetProperty("id").GetString() ?? throw new InvalidOperationException(); var name = r.TryGetProperty("ps", out var ps) ? ps.GetString() : null; return new(Guid.NewGuid(), string.IsNullOrWhiteSpace(name) ? $"VMess {host}" : name!, Protocol.Vmess, host, port, id, new Dictionary<string,string>()); }
    private static string TryDecodeSubscription(string text) { if (text.Contains("://")) return text; try { var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(Pad(text))); return decoded.Contains("://") ? decoded : text; } catch { return text; } }
    private static Dictionary<string, string> ParseQuery(string query) => query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('=', 2)).ToDictionary(x => Uri.UnescapeDataString(x[0]), x => x.Length == 2 ? Uri.UnescapeDataString(x[1]) : "", StringComparer.OrdinalIgnoreCase);
    private static void ValidateOptions(Protocol protocol, IReadOnlyDictionary<string, string> options)
    {
        var supported = protocol == Protocol.Vless
            ? new HashSet<string>(["type", "security", "sni", "flow", "encryption", "fp", "pbk", "sid", "spx", "path", "host", "serviceName", "headerType", "alpn", "allowInsecure", "insecure"], StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unknown = options.Keys.FirstOrDefault(x => !supported.Contains(x));
        if (unknown is not null) throw new InvalidOperationException($"Unsupported option: {unknown}.");
    }
    private static string Pad(string value) => value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4);
}
