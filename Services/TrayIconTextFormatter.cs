using WinMoe.Models;

namespace WinMoe.Services;

public static class TrayIconTextFormatter
{
    private const int NotifyIconTextLimit = 63;

    public static string BuildTooltip(SystemTelemetrySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "WinMoe - 准备中";
        }

        var text = $"WinMoe CPU {snapshot.CpuUsagePercent:0}% 内存 {snapshot.MemoryUsagePercent:0}%";
        return text.Length <= NotifyIconTextLimit ? text : text[..NotifyIconTextLimit];
    }

    public static string BuildHealthLine(SystemTelemetrySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "健康度：等待采样";
        }

        // Same check-based score as the dashboard and tray HUD.
        var (score, label, _) = SystemHealthEvaluator.Evaluate(snapshot);
        return $"健康度 {score} · {label}";
    }

    public static string BuildResourceLine(SystemTelemetrySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "CPU --  内存 --  磁盘 --";
        }

        var diskPercent = SystemHealthEvaluator.AggregateDiskUsagePercent(snapshot);
        return $"CPU {snapshot.CpuUsagePercent:0}%  内存 {snapshot.MemoryUsagePercent:0}%  磁盘 {diskPercent:0}%";
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
