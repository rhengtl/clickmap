using System.IO;

namespace ClickMap.Services;

/// <summary>
/// Minimal thread-safe file logger writing to <c>%APPDATA%\ClickMap\logs\</c>. Logging
/// must never throw into callers, so all I/O failures are swallowed.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private static string? _path;

    public static void Init(string baseDir)
    {
        try
        {
            string logDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logDir);
            _path = Path.Combine(logDir, $"clickmap-{DateTime.Now:yyyyMMdd}.log");
        }
        catch
        {
            _path = null;
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        System.Diagnostics.Debug.WriteLine(line);

        if (_path is null) return;
        try
        {
            lock (Gate)
                File.AppendAllText(_path, line + Environment.NewLine);
        }
        catch
        {
            // Never let logging break the app.
        }
    }
}
