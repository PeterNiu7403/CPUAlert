using WinMoe.Models;
using Xunit;

namespace WinMoe.Tests;

public sealed class ProcessTelemetryTests
{
    [Fact]
    public void PowerImpactText_UsesEmDash_WhenCpuIsIdle()
    {
        var process = new ProcessTelemetry("idle", 1, 1024, CpuUsagePercent: 0.5);
        Assert.Equal("—", process.PowerImpactText);
    }

    [Fact]
    public void PowerImpactText_UsesProxy_WhenCpuIsBusy()
    {
        var process = new ProcessTelemetry("busy", 2, 2048, CpuUsagePercent: 20);
        Assert.Equal("15", process.PowerImpactText);
    }

    [Fact]
    public void Initials_UsesFirstLetter()
    {
        Assert.Equal("C", new ProcessTelemetry("chrome", 3, 1).Initials);
        Assert.Equal("?", new ProcessTelemetry("  ", 4, 1).Initials);
    }

    [Fact]
    public void CpuBarWidth_ClampsToFortyDipScale()
    {
        Assert.Equal(40, new ProcessTelemetry("max", 5, 1, CpuUsagePercent: 100).CpuBarWidth);
        Assert.Equal(0, new ProcessTelemetry("zero", 6, 1, CpuUsagePercent: 0).CpuBarWidth);
    }
}
