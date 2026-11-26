namespace RismanNG.Infrastructure;
internal static class StartupTrace
{
    private static readonly string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RismanNG-startup.log");
    public static void Write(string message)
    {
        try { File.AppendAllText(Path, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}"); } catch { }
    }
    public static void Write(Exception exception) => Write($"{exception.GetType().Name}: {exception.Message}");
}
