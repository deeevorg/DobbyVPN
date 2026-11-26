using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;
namespace RismanNG.Infrastructure;
public sealed class WindowsProxyManager
{
    private const string KeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings";
    private readonly string _journal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RismanNG", "proxy-journal.json");
    private sealed record State(int Enable, string? Server, string Applied);
    public async Task RecoverAsync(CancellationToken ct = default) { if (!File.Exists(_journal)) return; var state = JsonSerializer.Deserialize<State>(await File.ReadAllTextAsync(_journal, ct).ConfigureAwait(false)); if (state is not null && CurrentServer() == state.Applied) Restore(state); TryDeleteJournal(); }
    public async Task ApplyAsync(string server, CancellationToken ct = default) { using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true) ?? throw new InvalidOperationException("Windows proxy settings are unavailable."); var state = new State(Convert.ToInt32(key.GetValue("ProxyEnable", 0)), key.GetValue("ProxyServer")?.ToString(), server); Directory.CreateDirectory(Path.GetDirectoryName(_journal)!); await File.WriteAllTextAsync(_journal, JsonSerializer.Serialize(state), ct); key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord); key.SetValue("ProxyServer", server, RegistryValueKind.String); Refresh(); }
    public async Task RestoreAsync(CancellationToken ct = default) { if (!File.Exists(_journal)) return; var state = JsonSerializer.Deserialize<State>(await File.ReadAllTextAsync(_journal, ct)); if (state is not null && CurrentServer() == state.Applied) Restore(state); TryDeleteJournal(); }
    private static string? CurrentServer() { using var key = Registry.CurrentUser.OpenSubKey(KeyPath); return key?.GetValue("ProxyServer")?.ToString(); }
    private static void Restore(State state) { using var key = Registry.CurrentUser.OpenSubKey(KeyPath, true); if (key is null) return; key.SetValue("ProxyEnable", state.Enable, RegistryValueKind.DWord); if (state.Server is null) key.DeleteValue("ProxyServer", false); else key.SetValue("ProxyServer", state.Server, RegistryValueKind.String); Refresh(); }
    private void TryDeleteJournal() { try { File.Delete(_journal); } catch { } }
    [DllImport("wininet.dll", SetLastError = true)] private static extern bool InternetSetOption(nint handle, int option, nint buffer, int length);
    private static void Refresh() { InternetSetOption(0, 39, 0, 0); InternetSetOption(0, 37, 0, 0); }
}
