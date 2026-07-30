using System.Diagnostics;

namespace WinMoe.Services;

/// <summary>
/// Best-effort "active now" hints for Apps rows by matching installed app names
/// against currently running process names (Mole shows green activity text).
/// </summary>
public static class AppActivityHintResolver
{
    public static IReadOnlyDictionary<string, string> ResolveForApps(
        IEnumerable<string> applicationNames,
        IEnumerable<string>? runningProcessNames = null)
    {
        var running = (runningProcessNames ?? CaptureRunningProcessNames())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(Normalize)
            .Where(name => name.Length >= 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawName in applicationNames)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                continue;
            }

            if (IsRunning(rawName, running))
            {
                map[rawName] = "使用中";
            }
        }

        return map;
    }

    public static bool IsRunning(string applicationName, ISet<string> runningNormalizedNames)
    {
        var app = Normalize(applicationName);
        if (app.Length < 2)
        {
            return false;
        }

        foreach (var process in runningNormalizedNames)
        {
            if (process.Length < 2)
            {
                continue;
            }

            // Exact or containment with a minimum length to limit false positives.
            if (string.Equals(app, process, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (process.Length >= 4 && app.Contains(process, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (app.Length >= 4 && process.Contains(app, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // First token match: "Visual Studio Code" ↔ "Code"
            var token = app.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            if (token.Length >= 4 &&
                (string.Equals(token, process, StringComparison.OrdinalIgnoreCase) ||
                 process.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<string> CaptureRunningProcessNames()
    {
        try
        {
            return Process.GetProcesses()
                .Select(process =>
                {
                    try
                    {
                        return process.ProcessName;
                    }
                    finally
                    {
                        process.Dispose();
                    }
                })
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string Normalize(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 4)
        {
            trimmed = trimmed[..^4];
        }

        return trimmed;
    }
}
