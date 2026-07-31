using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class BatteryDetailProbeTests
{
    // BATTERY_INFORMATION fixed offsets: DesignedCapacity=12, FullChargedCapacity=16, CycleCount=32.
    private const int DesignedCapacityOffset = 12;
    private const int FullChargedCapacityOffset = 16;
    private const int CycleCountOffset = 32;

    [Fact]
    public void ParseBatteryInformation_ValidBuffer_ReadsCapacitiesAndCycles()
    {
        var buffer = new byte[36];
        BitConverter.GetBytes(90_000u).CopyTo(buffer, DesignedCapacityOffset);
        BitConverter.GetBytes(85_990u).CopyTo(buffer, FullChargedCapacityOffset);
        BitConverter.GetBytes(7u).CopyTo(buffer, CycleCountOffset);

        var snapshot = BatteryDetailProbe.ParseBatteryInformation(buffer);

        Assert.Equal(90_000, snapshot.DesignCapacityMwh);
        Assert.Equal(85_990, snapshot.FullChargeCapacityMwh);
        Assert.Equal(7, snapshot.CycleCount);
        Assert.Null(snapshot.RateMw);
    }

    [Fact]
    public void ParseBatteryInformation_UnknownSentinels_MapToNull()
    {
        var buffer = new byte[36];
        BitConverter.GetBytes(uint.MaxValue).CopyTo(buffer, DesignedCapacityOffset);
        BitConverter.GetBytes(uint.MaxValue).CopyTo(buffer, FullChargedCapacityOffset);
        // CycleCount 0 means "not reported" — Query falls back to WMI BatteryCycleCount.
        BitConverter.GetBytes(0u).CopyTo(buffer, CycleCountOffset);

        var snapshot = BatteryDetailProbe.ParseBatteryInformation(buffer);

        Assert.Null(snapshot.DesignCapacityMwh);
        Assert.Null(snapshot.FullChargeCapacityMwh);
        Assert.Null(snapshot.CycleCount);
    }

    [Fact]
    public void ParseBatteryInformation_ShortBuffer_ReturnsEmptySnapshot()
    {
        var snapshot = BatteryDetailProbe.ParseBatteryInformation(new byte[8]);

        Assert.Equal(new BatteryDetailSnapshot(null, null, null, null), snapshot);
    }
}
