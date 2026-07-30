using WinMoe.Models;
using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class TrayIconTextFormatterTests
{
    [Fact]
    public void BuildTooltip_ReturnsWarmupText_WhenSnapshotIsMissing()
    {
        Assert.Equal("WinMoe - 准备中", TrayIconTextFormatter.BuildTooltip(null));
    }

    [Fact]
    public void BuildTooltip_IncludesCpuAndMemoryPercentages()
    {
        var snapshot = new SystemTelemetrySnapshot(
            DateTimeOffset.Parse("2026-06-15T00:00:00Z"),
            24.2,
            51.8,
            4,
            8,
            70,
            3,
            4,
            100,
            50,
            "GPU pending",
            []);

        Assert.Equal("WinMoe CPU 24% 内存 52%", TrayIconTextFormatter.BuildTooltip(snapshot));
    }

    [Fact]
    public void BuildMenuLines_ReturnStatusText_WhenSnapshotIsMissing()
    {
        Assert.Equal("健康度：等待采样", TrayIconTextFormatter.BuildHealthLine(null));
        Assert.Equal("CPU --  内存 --  磁盘 --", TrayIconTextFormatter.BuildResourceLine(null));
        Assert.Equal("网络 --", TrayIconTextFormatter.BuildNetworkLine(null));
        Assert.Equal("尚无遥测采样", TrayIconTextFormatter.BuildSampleLine(null));
    }

    [Fact]
    public void BuildMenuLines_FormatLatestSnapshot()
    {
        var snapshot = new SystemTelemetrySnapshot(
            DateTimeOffset.Parse("2026-06-15T08:30:05Z"),
            24.2,
            51.8,
            4,
            8,
            70,
            3,
            4,
            2048,
            1024,
            "GPU pending",
            []);

        Assert.Equal("健康度 65 · 需关注", TrayIconTextFormatter.BuildHealthLine(snapshot));
        Assert.Equal("CPU 24%  内存 52%  磁盘 70%", TrayIconTextFormatter.BuildResourceLine(snapshot));
        Assert.Equal("网络 ↓ 2 KB/s / ↑ 1 KB/s", TrayIconTextFormatter.BuildNetworkLine(snapshot));
        Assert.Contains("最新采样", TrayIconTextFormatter.BuildSampleLine(snapshot));
    }
}
