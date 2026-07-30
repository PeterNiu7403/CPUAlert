using System.Globalization;
using System.Text.RegularExpressions;
using WinMoe.Models;

namespace WinMoe.Services;

public sealed record LifetimeStats(
    long CleanedBytes,
    int CleanOperations,
    int UninstallCount,
    int OptimizeCount)
{
    public static LifetimeStats Empty { get; } = new(0, 0, 0, 0);

    public string CleanedText => CleanedBytes > 0
        ? SystemTelemetryFormatter.Bytes(CleanedBytes)
        : CleanOperations > 0 ? CleanOperations.ToString(CultureInfo.InvariantCulture) : "—";

    public string UninstalledText => UninstallCount > 0
        ? UninstallCount.ToString(CultureInfo.InvariantCulture)
        : "—";

    public string OptimizedText => OptimizeCount > 0
        ? OptimizeCount.ToString(CultureInfo.InvariantCulture)
        : "—";
}

public static partial class LifetimeStatsAggregator
{
    private static readonly Regex FreedBytesPattern = FreedBytesRegex();
    private static readonly Regex FreedHumanPattern = FreedHumanRegex();

    public static LifetimeStats Aggregate(IEnumerable<OperationHistoryEntry> entries)
    {
        long cleanedBytes = 0;
        var cleanOps = 0;
        var uninstalls = 0;
        var optimizes = 0;

        foreach (var entry in entries)
        {
            if (!entry.Succeeded)
            {
                continue;
            }

            var operation = Normalize(entry.Operation);
            if (IsCleanOperation(operation))
            {
                cleanOps++;
                cleanedBytes += ExtractBytes(entry.Summary);
            }
            else if (IsUninstallOperation(operation))
            {
                uninstalls++;
            }
            else if (IsOptimizeOperation(operation))
            {
                optimizes++;
            }
        }

        return new LifetimeStats(cleanedBytes, cleanOps, uninstalls, optimizes);
    }

    private static bool IsCleanOperation(string operation) =>
        operation is "clean" or "remove_leftovers" or "purge" or "installer";

    private static bool IsUninstallOperation(string operation) =>
        operation is "uninstall" or "remove" or "launch_uninstaller";

    private static bool IsOptimizeOperation(string operation) =>
        operation is "optimize" or "optimise";

    private static string Normalize(string operation) =>
        (operation ?? string.Empty).Trim().TrimStart('-').ToLowerInvariant();

    private static long ExtractBytes(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return 0;
        }

        var freed = FreedBytesPattern.Match(summary);
        if (freed.Success && long.TryParse(freed.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw))
        {
            return raw;
        }

        var human = FreedHumanPattern.Match(summary);
        if (human.Success &&
            double.TryParse(human.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
        {
            var unit = human.Groups[2].Value.ToUpperInvariant();
            return unit switch
            {
                "TB" => (long)(amount * 1_099_511_627_776d),
                "GB" => (long)(amount * 1_073_741_824d),
                "MB" => (long)(amount * 1_048_576d),
                "KB" => (long)(amount * 1024d),
                _ => (long)amount
            };
        }

        return 0;
    }

    [GeneratedRegex(@"Freed\s+(\d+)\s+bytes", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FreedBytesRegex();

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*(TB|GB|MB|KB|B)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FreedHumanRegex();
}
