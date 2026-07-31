using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class BatteryDetailFormatterTests
{
    [Fact]
    public void ComputeHealthPercent_NormalPack_RoundsToWholePercent()
    {
        Assert.Equal(96, BatteryDetailFormatter.ComputeHealthPercent(90_000, 85_990));
    }

    [Fact]
    public void ComputeHealthPercent_FullEqualsDesign_Is100()
    {
        Assert.Equal(100, BatteryDetailFormatter.ComputeHealthPercent(90_000, 90_000));
    }

    [Fact]
    public void ComputeHealthPercent_MissingOrZeroCapacities_ReturnsNull()
    {
        Assert.Null(BatteryDetailFormatter.ComputeHealthPercent(null, 85_990));
        Assert.Null(BatteryDetailFormatter.ComputeHealthPercent(90_000, null));
        Assert.Null(BatteryDetailFormatter.ComputeHealthPercent(null, null));
        Assert.Null(BatteryDetailFormatter.ComputeHealthPercent(0, 85_990));
        Assert.Null(BatteryDetailFormatter.ComputeHealthPercent(90_000, 0));
    }

    [Fact]
    public void ComputeHealthPercent_FullFarAboveDesign_TreatedAsGarbage()
    {
        Assert.Null(BatteryDetailFormatter.ComputeHealthPercent(90_000, 120_000));
        Assert.Equal(100, BatteryDetailFormatter.ComputeHealthPercent(90_000, 108_000));
    }

    [Fact]
    public void ComputeHealthPercent_ClampsIntoOneToHundred()
    {
        Assert.Equal(100, BatteryDetailFormatter.ComputeHealthPercent(90_000, 95_000));
        Assert.Equal(1, BatteryDetailFormatter.ComputeHealthPercent(90_000, 100));
    }

    [Fact]
    public void BuildBadgeText_HealthPercent_IncludesValue()
    {
        Assert.Equal("健康 96%", BatteryDetailFormatter.BuildBadgeText(96, true));
    }

    [Fact]
    public void BuildBadgeText_NoHealth_FallsBackToPlainLabel()
    {
        Assert.Equal("健康", BatteryDetailFormatter.BuildBadgeText(null, true));
    }

    [Fact]
    public void BuildBadgeText_NoBattery_IsEmpty()
    {
        Assert.Equal(string.Empty, BatteryDetailFormatter.BuildBadgeText(96, false));
        Assert.Equal(string.Empty, BatteryDetailFormatter.BuildBadgeText(null, false));
    }

    [Fact]
    public void BuildFooterText_ChargeRate_FormatsWatts()
    {
        Assert.Equal("⚡65W", BatteryDetailFormatter.BuildFooterText(65_000, null, "充电中", string.Empty));
    }

    [Fact]
    public void BuildFooterText_DischargeRate_UsesAbsoluteValue()
    {
        Assert.Equal("⚡12W", BatteryDetailFormatter.BuildFooterText(-12_000, null, "放电中", string.Empty));
    }

    [Fact]
    public void BuildFooterText_CyclesOnly()
    {
        Assert.Equal("7 次循环", BatteryDetailFormatter.BuildFooterText(null, 7, "已接电源", string.Empty));
    }

    [Fact]
    public void BuildFooterText_JoinsAllSegments()
    {
        Assert.Equal(
            "⚡12W · 7 次循环 · 预计剩余 3 小时",
            BatteryDetailFormatter.BuildFooterText(-12_000, 7, "放电中", "预计剩余 3 小时"));
    }

    [Fact]
    public void BuildFooterText_AllEmpty_StaysEmpty()
    {
        Assert.Equal(string.Empty, BatteryDetailFormatter.BuildFooterText(null, null, "已接电源", string.Empty));
    }

    [Fact]
    public void BuildFooterText_SubWattRate_DropsSegment()
    {
        Assert.Equal(string.Empty, BatteryDetailFormatter.BuildFooterText(400, null, "充电中", string.Empty));
    }
}
