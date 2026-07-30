using MoleWindows.Models;

namespace MoleWindows.Services;

public static class TrayIconTextFormatter
{
    private const int NotifyIconTextLimit = 63;

    public static string BuildTooltip(SystemTelemetrySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "Mole Windows - 准备中";
        }

        var text = $"Mole Windows CPU {snapshot.CpuUsagePercent:0}% 内存 {snapshot.MemoryUsagePercent:0}%";
        return text.Length <= NotifyIconTextLimit ? text : text[..NotifyIconTextLimit];
    }

    public static string BuildHealthLine(SystemTelemetrySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "健康度：等待采样";
        }

        var pressure = Math.Max(snapshot.CpuUsagePercent, Math.Max(snapshot.MemoryUsagePercent, snapshot.DiskUsagePercent));
        var score = Math.Clamp(100 - (int)Math.Round(pressure / 2), 0, 100);
        var label = score >= 80 ? "良好" : score >= 60 ? "需关注" : "繁忙";
        return $"健康度 {score} · {label}";
    }

    public static string BuildResourceLine(SystemTelemetrySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "CPU --  内存 --  磁盘 --";
        }

        return $"CPU {snapshot.CpuUsagePercent:0}%  内存 {snapshot.MemoryUsagePercent:0}%  磁盘 {snapshot.DiskUsagePercent:0}%";
    }

    public static string BuildNetworkLine(SystemTelemetrySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "网络 --";
        }

        var received = SystemTelemetryFormatter.Rate(snapshot.NetworkReceivedBytesPerSecond);
        var sent = SystemTelemetryFormatter.Rate(snapshot.NetworkSentBytesPerSecond);
        return $"网络 ↓ {received} / ↑ {sent}";
    }

    public static string BuildSampleLine(SystemTelemetrySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "尚无遥测采样";
        }

        return $"最新采样 {snapshot.CapturedAt.ToLocalTime():HH:mm:ss}";
    }
}
