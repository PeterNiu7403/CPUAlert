namespace WinMoe.Services;

public sealed class LocalStartupDiagnosticsService : IStartupDiagnosticsService
{
    public LocalStartupDiagnosticsService()
        : this(ApplicationDataPaths.CurrentFile("startup.log"))
    {
    }

    public LocalStartupDiagnosticsService(string logPath)
    {
        LogPath = logPath;
    }

    public string LogPath { get; }

    public void Record(string phase, string message)
    {
        WriteLine(FormatLine(DateTimeOffset.Now, phase, message));
    }

    public void RecordException(string phase, Exception exception)
    {
        // Full ToString() keeps HResult, inner exceptions and the stack trace;
        // FormatLine flattens newlines so the entry stays on a single log line.
        Record(phase, exception.ToString());
    }

    public static string FormatLine(DateTimeOffset timestamp, string phase, string message)
    {
        var safePhase = Normalize(phase);
        var safeMessage = Normalize(message);
        return $"{timestamp:O} [{safePhase}] {safeMessage}";
    }

    private void WriteLine(string line)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch
        {
        }
    }

    private static string Normalize(string value)
    {
        return value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }
}
