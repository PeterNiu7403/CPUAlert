using System.Globalization;
using WinMoe.Models;

namespace WinMoe.Services;

public static class TrayHudStatusFormatter
{
    public static TrayHudStatus Build(
        SystemTelemetrySnapshot? snapshot,
        OperationHistoryEntry? activity,
        IEnumerable<OperationHistoryEntry>? lifetimeEntries = null)
    {
        var telemetry = BuildTelemetry(snapshot);
        var activityText = BuildActivity(activity);
        var lifetime = lifetimeEntries is null
            ? LifetimeStats.Empty
            : LifetimeStatsAggregator.Aggregate(lifetimeEntries);

        return new TrayHudStatus(
            telemetry.SampleText,
            telemetry.HealthScore,
            telemetry.HealthLabel,
            telemetry.CpuText,
            telemetry.MemoryText,
            telemetry.DiskText,
            telemetry.NetworkText,
            activityText.ActivityTitle,
            activityText.ActivityDetail,
            telemetry.TopProcesses,
            lifetime.CleanedText,
            lifetime.UninstalledText,
            lifetime.OptimizedText,
            telemetry.DeviceChipText,
            telemetry.GpuText,
            telemetry.FanText,
            telemetry.MemoryDetailText,
            telemetry.DiskDetailText,
            telemetry.NetworkDetailText);
    }

    private static (
        string SampleText,
        string HealthScore,
        string HealthLabel,
        string CpuText,
        string MemoryText,
        string DiskText,
        string NetworkText,
        IReadOnlyList<ProcessTelemetry> TopProcesses,
        string DeviceChipText,
        string GpuText,
        string FanText,
        string MemoryDetailText,
        string DiskDetailText,
        string NetworkDetailText) BuildTelemetry(SystemTelemetrySnapshot? snapshot)
    {
        var device = BuildDeviceChip(snapshot);
        if (snapshot is null)
        {
            return (
                "尚无遥测采样",
                "--",
                "准备中",
                "--",
                "--",
                "--",
                "--",
                [],
                device,
                "—",
                "—",
                "",
                "",
                "");
        }

        var pressure = Math.Max(snapshot.CpuUsagePercent, Math.Max(snapshot.MemoryUsagePercent, snapshot.DiskUsagePercent));
        var score = Math.Clamp(100 - (int)Math.Round(pressure / 2), 0, 100);
        var topProcesses = snapshot.TopProcesses
            .OrderByDescending(process => process.CpuUsagePercent)
            .ThenByDescending(process => process.WorkingSetBytes)
            .Take(5)
            .ToArray();

        var freeDisk = Math.Max(0, snapshot.DiskTotalBytes - snapshot.DiskUsedBytes);
        var gpu = string.IsNullOrWhiteSpace(snapshot.GpuStatus) ||
                  string.Equals(snapshot.GpuStatus, "Unavailable", StringComparison.OrdinalIgnoreCase)
            ? "—"
            : snapshot.GpuStatus;

        return (
            $"更新于 {snapshot.CapturedAt.ToLocalTime():HH:mm:ss}",
            score.ToString(CultureInfo.InvariantCulture),
            score >= 80 ? "各项指标正常" : score >= 60 ? "需关注" : "繁忙",
            SystemTelemetryFormatter.Percent(snapshot.CpuUsagePercent),
            SystemTelemetryFormatter.Percent(snapshot.MemoryUsagePercent),
            SystemTelemetryFormatter.Percent(snapshot.DiskUsagePercent),
            SystemTelemetryFormatter.Rate(
                snapshot.NetworkReceivedBytesPerSecond + snapshot.NetworkSentBytesPerSecond),
            topProcesses,
            device,
            gpu,
            "—",
            SystemTelemetryFormatter.MemorySummary(snapshot),
            $"可用 {SystemTelemetryFormatter.Bytes(freeDisk)}",
            $"↓ {SystemTelemetryFormatter.Rate(snapshot.NetworkReceivedBytesPerSecond)}  ↑ {SystemTelemetryFormatter.Rate(snapshot.NetworkSentBytesPerSecond)}");
    }

    private static string BuildDeviceChip(SystemTelemetrySnapshot? snapshot)
    {
        var machine = Environment.MachineName;
        if (string.IsNullOrWhiteSpace(machine))
        {
            machine = "Windows";
        }

        if (snapshot is null || snapshot.MemoryTotalBytes <= 0)
        {
            return machine;
        }

        return $"{machine} · {SystemTelemetryFormatter.Bytes(snapshot.MemoryTotalBytes)}";
    }

    private static (string ActivityTitle, string ActivityDetail) BuildActivity(OperationHistoryEntry? activity)
    {
        if (activity is null)
        {
            return ("暂无活动", "WinMoe 尚未记录操作。");
        }

        return (
            $"{activity.Operation} · {activity.ResultText}",
            $"{activity.TimestampUtc.ToLocalTime():HH:mm:ss} · {activity.Summary}");
    }
}
