using System.Diagnostics;

namespace OfflineAssistant;

internal static class AppLog
{
    private static readonly object Gate = new();
    public static string FilePath => Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "sphere.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warning(string message) => Write("WARN", message);
    public static void Error(string message, Exception exception) => Write("ERROR", $"{message}: {exception}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
                File.AppendAllText(FilePath, $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Cannot write {FilePath}: {ex}");
        }
    }
}
