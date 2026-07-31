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
            telemetry.NetworkDetailText,
            telemetry.GpuDetailText,
            telemetry.FanDetailText);
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
        string NetworkDetailText,
        string GpuDetailText,
        string FanDetailText) BuildTelemetry(SystemTelemetrySnapshot? snapshot)
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
                "",
                "",
                "");
        }

        // Same check-based score as the dashboard (SystemHealthEvaluator).
        var (score, healthLabel, _) = SystemHealthEvaluator.Evaluate(snapshot);
        var topProcesses = snapshot.TopProcesses
            .OrderByDescending(process => process.CpuUsagePercent)
            .ThenByDescending(process => process.WorkingSetBytes)
            .Take(12)
            .ToArray();

        // Disk: aggregate every fixed volume (Mole shows whole-disk free/total).
        double diskPercent;
        long diskFree;
        if (snapshot.AllDisksTotalBytes > 0)
        {
            diskPercent = SystemHealthEvaluator.AggregateDiskUsagePercent(snapshot);
            diskFree = snapshot.AllDisksFreeBytes;
        }
        else
        {
            diskPercent = snapshot.DiskUsagePercent;
            diskFree = Math.Max(0, snapshot.DiskTotalBytes - snapshot.DiskUsedBytes);
        }

        var (gpu, gpuDetail) = BuildGpuSurface(snapshot);
        var (fan, fanDetail) = BuildFanSurface(snapshot);

        return (
            $"更新于 {snapshot.CapturedAt.ToLocalTime():HH:mm:ss}",
            score,
            healthLabel,
            SystemTelemetryFormatter.Percent(snapshot.CpuUsagePercent),
            SystemTelemetryFormatter.Percent(snapshot.MemoryUsagePercent),
            SystemTelemetryFormatter.Percent(diskPercent),
            SystemTelemetryFormatter.Rate(
                snapshot.NetworkReceivedBytesPerSecond + snapshot.NetworkSentBytesPerSecond),
            topProcesses,
            device,
            gpu,
            fan,
            SystemTelemetryFormatter.MemorySummary(snapshot),
            $"可用 {SystemTelemetryFormatter.Bytes(diskFree)}",
            $"↓ {SystemTelemetryFormatter.Rate(snapshot.NetworkReceivedBytesPerSecond)}  ↑ {SystemTelemetryFormatter.Rate(snapshot.NetworkSentBytesPerSecond)}",
            gpuDetail,
            fanDetail);
    }

    private static (string Gpu, string Detail) BuildGpuSurface(SystemTelemetrySnapshot snapshot)
    {
        var discrete = snapshot.GpuAdapters.FirstOrDefault(adapter => adapter.Kind == GpuAdapterKind.Discrete);
        var integrated = snapshot.GpuAdapters.FirstOrDefault(adapter => adapter.Kind == GpuAdapterKind.Integrated);
        var primary = discrete ?? integrated ?? snapshot.GpuAdapters.FirstOrDefault();

        if (primary is not null)
        {
            var detail = discrete is not null && integrated is not null
                ? $"独显 {discrete.ShortName} · 集显 {integrated.Engine3DPercent:0.0}%"
                : discrete is not null
                    ? $"独显 {discrete.ShortName}"
                    : $"集显 {primary.ShortName}";
            if (primary.TemperatureCelsius is { } temperature)
            {
                detail = string.Create(CultureInfo.InvariantCulture, $"{detail} · {temperature:0}°C");
            }

            return (string.Create(CultureInfo.InvariantCulture, $"{primary.Engine3DPercent:0.0}%"), detail);
        }

        var fallback = string.IsNullOrWhiteSpace(snapshot.GpuStatus) ||
                       string.Equals(snapshot.GpuStatus, "Unavailable", StringComparison.OrdinalIgnoreCase)
            ? "—"
            : snapshot.GpuStatus;
        return (fallback, fallback == "—" ? "引擎计数不可用" : "Windows 采样");
    }

    private static (string Fan, string Detail) BuildFanSurface(SystemTelemetrySnapshot snapshot)
    {
        if (snapshot.Fans.Count == 0)
        {
            return ("—", "未暴露转速接口");
        }

        var maxRpm = snapshot.Fans.Max(fan => fan.Rpm);
        var detail = string.Join(" · ", snapshot.Fans.Select(fan => $"{fan.Name} {fan.Rpm}"));
        detail = snapshot.FanMaxRpm is { } peak && peak > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{detail} RPM · 负载 {(int)Math.Round(maxRpm * 100d / peak)}%")
            : $"{detail} RPM";
        return (maxRpm.ToString(CultureInfo.InvariantCulture), detail);
    }

    private static string BuildDeviceChip(SystemTelemetrySnapshot? snapshot)
    {
        if (snapshot is null || snapshot.MemoryTotalBytes <= 0)
        {
            return CpuModelNameResolver.Get();
        }

        // Mole device chip: "M5 Pro · 48 GB" → CPU model + RAM.
        return $"{CpuModelNameResolver.Get()} · {SystemTelemetryFormatter.Bytes(snapshot.MemoryTotalBytes)}";
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
