using MoleWindows.Models;

namespace MoleWindows.Services;

public static class TrayHudStatusFormatter
{
    public static TrayHudStatus Build(SystemTelemetrySnapshot? snapshot, OperationHistoryEntry? activity)
    {
        var telemetry = BuildTelemetry(snapshot);
        var activityText = BuildActivity(activity);

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
            telemetry.TopProcesses);
    }

    private static (
        string SampleText,
        string HealthScore,
        string HealthLabel,
        string CpuText,
        string MemoryText,
        string DiskText,
        string NetworkText,
        IReadOnlyList<ProcessTelemetry> TopProcesses) BuildTelemetry(SystemTelemetrySnapshot? snapshot)
    {
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
                []);
        }

        var pressure = Math.Max(snapshot.CpuUsagePercent, Math.Max(snapshot.MemoryUsagePercent, snapshot.DiskUsagePercent));
        var score = Math.Clamp(100 - (int)Math.Round(pressure / 2), 0, 100);
        var topProcesses = snapshot.TopProcesses
            .OrderByDescending(process => process.CpuUsagePercent)
            .ThenByDescending(process => process.WorkingSetBytes)
            .Take(4)
            .ToArray();

        return (
            $"更新于 {snapshot.CapturedAt.ToLocalTime():HH:mm:ss}",
            score.ToString(),
            score >= 80 ? "良好" : score >= 60 ? "需关注" : "繁忙",
            SystemTelemetryFormatter.Percent(snapshot.CpuUsagePercent),
            SystemTelemetryFormatter.Percent(snapshot.MemoryUsagePercent),
            SystemTelemetryFormatter.Percent(snapshot.DiskUsagePercent),
            $"↓ {SystemTelemetryFormatter.Rate(snapshot.NetworkReceivedBytesPerSecond)} / ↑ {SystemTelemetryFormatter.Rate(snapshot.NetworkSentBytesPerSecond)}",
            topProcesses);
    }

    private static (string ActivityTitle, string ActivityDetail) BuildActivity(OperationHistoryEntry? activity)
    {
        if (activity is null)
        {
            return ("暂无活动", "Mole Windows 尚未记录操作。");
        }

        return (
            $"{activity.Operation} - {activity.ResultText}",
            $"{activity.TimestampUtc.ToLocalTime():HH:mm:ss} - {activity.Summary}");
    }
}
