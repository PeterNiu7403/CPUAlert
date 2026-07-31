using System.Globalization;
using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// Single source of truth for the health score so the dashboard, tray HUD and
/// tray tooltip never drift apart. Check-based: start at 100, subtract per failed check.
/// </summary>
public static class SystemHealthEvaluator
{
    public static (string Score, string Status, string Reason) Evaluate(SystemTelemetrySnapshot snapshot)
    {
        var penalty = 0;
        string? reason = null;

        var diskPercent = snapshot.AllDisksTotalBytes > 0
            ? (snapshot.AllDisksTotalBytes - snapshot.AllDisksFreeBytes) * 100d / snapshot.AllDisksTotalBytes
            : snapshot.DiskUsagePercent;
        if (diskPercent >= 95)
        {
            penalty += 40;
            reason = "磁盘空间即将用尽";
        }
        else if (diskPercent >= 90)
        {
            penalty += 20;
            reason = "磁盘空间偏低";
        }

        if (snapshot.MemoryUsagePercent >= 90)
        {
            penalty += 20;
            reason ??= "内存压力偏高";
        }

        if (snapshot.CpuUsagePercent >= 90)
        {
            penalty += 10;
            reason ??= "CPU 负载偏高";
        }

        if (snapshot.HasBattery &&
            snapshot.BatteryChargePercent is <= 20 &&
            string.Equals(snapshot.BatteryStatusText, "discharging", StringComparison.OrdinalIgnoreCase))
        {
            penalty += 10;
            reason ??= "电池电量低";
        }

        var score = Math.Clamp(100 - penalty, 0, 100);
        var status = score >= 90 ? "各项指标正常" : score >= 60 ? "需关注" : "繁忙";
        return (score.ToString("0", CultureInfo.InvariantCulture), status, reason ?? "检查项均通过");
    }

    /// <summary>Aggregate disk usage across every fixed volume, falling back to the system drive.</summary>
    public static double AggregateDiskUsagePercent(SystemTelemetrySnapshot snapshot)
    {
        return snapshot.AllDisksTotalBytes > 0
            ? (snapshot.AllDisksTotalBytes - snapshot.AllDisksFreeBytes) * 100d / snapshot.AllDisksTotalBytes
            : snapshot.DiskUsagePercent;
    }
}
