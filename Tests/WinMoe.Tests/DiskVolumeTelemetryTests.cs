using WinMoe.Models;
using Xunit;

namespace WinMoe.Tests;

public sealed class DiskVolumeTelemetryTests
{
    [Fact]
    public void ComputedProperties_FormatPartitionSummary()
    {
        var volume = new DiskVolumeTelemetry(@"C:\", "Windows", 200, 50);

        Assert.Equal("C:", volume.LetterText);
        Assert.Equal(150, volume.UsedBytes);
        Assert.Equal(75, volume.UsagePercent);
        Assert.Equal("Windows (C:)", volume.DisplayName);
        Assert.Equal("150 B / 200 B", volume.UsedOverTotalText);
        Assert.Equal("150 B / 200 B · 可用 50 B", volume.SummaryText);
    }

    [Fact]
    public void DisplayName_FallsBackToLetter_WhenLabelIsEmpty()
    {
        var volume = new DiskVolumeTelemetry(@"D:\", string.Empty, 1024, 1024);

        Assert.Equal("D:", volume.DisplayName);
        Assert.Equal(0, volume.UsagePercent);
    }

    [Fact]
    public void UsagePercent_IsZero_WhenTotalIsZero()
    {
        var volume = new DiskVolumeTelemetry(@"E:\", "Data", 0, 0);

        Assert.Equal(0, volume.UsagePercent);
        Assert.Equal(0, volume.UsedBytes);
    }

    [Fact]
    public void UsedBytes_NeverNegative_WhenFreeExceedsTotal()
    {
        var volume = new DiskVolumeTelemetry(@"F:\", string.Empty, 100, 250);

        Assert.Equal(0, volume.UsedBytes);
        Assert.Equal(0, volume.UsagePercent);
    }
}
