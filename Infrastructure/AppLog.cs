using System.Text.RegularExpressions;
namespace RismanNG.Infrastructure;
public static partial class AppLog
{
    private static readonly object Gate = new();
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RismanNG", "logs");
    public static string FilePath => Path.Combine(DirectoryPath, "app.log");
    public static void Write(string area, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 256_000) File.Move(FilePath, Path.Combine(DirectoryPath, "app.previous.log"), true);
                File.AppendAllText(FilePath, $"{DateTimeOffset.UtcNow:O} [{area}] {Redact(message)}{Environment.NewLine}");
            }
        }
        catch { }
    }
    public static string Redact(string value) => Url().Replace(Uuid().Replace(value, "[redacted-uuid]"), "[redacted-url]");
    [GeneratedRegex(@"(?i)(vless|vmess|trojan|ss|hy2|hysteria2)://\S+")] private static partial Regex Url();
    [GeneratedRegex(@"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b")] private static partial Regex Uuid();
}
