namespace WinMoe.Models;

public sealed class WinMoeSettings
{
    public const int DefaultSamplingIntervalSeconds = 60;
    public const int DefaultHistoryRetentionDays = 90;
    public const int DefaultHttpServerPort = 9277;
    public const int MaxPinnedProcessNames = 24;

    public int SamplingIntervalSeconds { get; set; } = DefaultSamplingIntervalSeconds;

    public int HistoryRetentionDays { get; set; } = DefaultHistoryRetentionDays;

    public bool HttpServerEnabled { get; set; } = true;

    public int HttpServerPort { get; set; } = DefaultHttpServerPort;

    public bool TrayIconEnabled { get; set; } = true;

    public bool McpDestructiveActionsEnabled { get; set; }

    /// Share anonymous crash reports + usage analytics. Opt-out (on by default),
    /// matching the macOS app; see Services/AppTelemetry.cs.
    public bool TelemetryEnabled { get; set; } = true;

    /// <summary>
    /// Process names pinned to the top of the Status process table (case-insensitive).
    /// Persisted so pins survive app restarts; PIDs alone are unstable across reboots.
    /// </summary>
    public List<string> PinnedProcessNames { get; set; } = [];

    public static WinMoeSettings Normalize(WinMoeSettings? settings)
    {
        settings ??= new WinMoeSettings();
        return new WinMoeSettings
        {
            SamplingIntervalSeconds = Math.Clamp(settings.SamplingIntervalSeconds, 5, 300),
            HistoryRetentionDays = Math.Clamp(settings.HistoryRetentionDays, 1, 365),
            HttpServerEnabled = settings.HttpServerEnabled,
            HttpServerPort = Math.Clamp(settings.HttpServerPort, 1024, 65535),
            TrayIconEnabled = settings.TrayIconEnabled,
            McpDestructiveActionsEnabled = settings.McpDestructiveActionsEnabled,
            TelemetryEnabled = settings.TelemetryEnabled,
            PinnedProcessNames = NormalizePinnedProcessNames(settings.PinnedProcessNames)
        };
    }

    public static List<string> NormalizePinnedProcessNames(IEnumerable<string>? names)
    {
        if (names is null)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var raw in names)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var name = raw.Trim();
            if (name.Length > 128)
            {
                name = name[..128];
            }

            // Strip .exe for stable matching with Process.ProcessName.
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Length > 4)
            {
                name = name[..^4];
            }

            if (!seen.Add(name))
            {
                continue;
            }

            result.Add(name);
            if (result.Count >= MaxPinnedProcessNames)
            {
                break;
            }
        }

        return result;
    }
}
