using WinMoe.Models;
using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

/// <summary>Merge policy for the universal sensor probe chain.</summary>
public sealed class HardwareSensorMergerTests
{
    private static KeyValuePair<string, SensorProbeResult> Result(string name, SensorProbeResult result) =>
        new(name, result);

    [Fact]
    public void Merge_NoReadings_ReturnsUnavailable()
    {
        var sample = HardwareSensorMerger.Merge([Result("P1", SensorProbeResult.Empty)]);

        Assert.Same(HardwareSensorSample.Unavailable, sample);
        Assert.False(sample.HasAnyReading);
    }

    [Fact]
    public void Merge_CpuTemperature_FirstProviderWins()
    {
        var lenovo = new SensorProbeResult(61, null, [], [], null, []);
        var thermalZone = new SensorProbeResult(58, null, [], [], null, []);

        var sample = HardwareSensorMerger.Merge([Result("Lenovo GameZone", lenovo), Result("ACPI Thermal Zone", thermalZone)]);

        Assert.Equal(61, sample.CpuTemperatureCelsius);
    }

    [Fact]
    public void Merge_CpuTemperature_FallsBackToThermalZone()
    {
        var thermalZone = new SensorProbeResult(58, null, [], [], null, []);

        var sample = HardwareSensorMerger.Merge([Result("Lenovo GameZone", SensorProbeResult.Empty), Result("ACPI Thermal Zone", thermalZone)]);

        Assert.Equal(58, sample.CpuTemperatureCelsius);
    }

    [Fact]
    public void Merge_AggregateGpuTemperature_PrefersHottestVendorReading()
    {
        var nvapi = new SensorProbeResult(null, null,
            [new GpuSensorReading(0x10DE, "NVIDIA GeForce RTX 5080", 47, null)], [], null, []);
        var lenovo = new SensorProbeResult(null, 44, [], [], null, []);

        var sample = HardwareSensorMerger.Merge([Result("Lenovo GameZone", lenovo), Result("NVAPI", nvapi)]);

        Assert.Equal(47, sample.GpuTemperatureCelsius);
        Assert.Single(sample.GpuReadings);
    }

    [Fact]
    public void Merge_AggregateGpuTemperature_FallsBackToPlatformScalar()
    {
        var lenovo = new SensorProbeResult(null, 44, [], [], null, []);

        var sample = HardwareSensorMerger.Merge([Result("Lenovo GameZone", lenovo)]);

        Assert.Equal(44, sample.GpuTemperatureCelsius);
    }

    [Fact]
    public void Merge_VendorGpuFan_SkippedWhenPlatformReportsGpuFan()
    {
        var lenovo = new SensorProbeResult(null, null, [], [new FanSensorSample("GPU", 1800)], null, []);
        var nvapi = new SensorProbeResult(null, null,
            [new GpuSensorReading(0x10DE, "NVIDIA GeForce RTX 5080", 47, 2100)], [], null, []);

        var sample = HardwareSensorMerger.Merge([Result("Lenovo GameZone", lenovo), Result("NVAPI", nvapi)]);

        Assert.Single(sample.Fans);
        Assert.Equal(1800, sample.Fans[0].Rpm);
    }

    [Fact]
    public void Merge_VendorGpuFan_AddedWhenPlatformHasNone()
    {
        var nvapi = new SensorProbeResult(null, null,
            [new GpuSensorReading(0x10DE, "NVIDIA GeForce RTX 5080", 47, 2100)], [], null, []);

        var sample = HardwareSensorMerger.Merge([Result("NVAPI", nvapi)]);

        Assert.Single(sample.Fans);
        Assert.Equal("GPU", sample.Fans[0].Name);
        Assert.Equal(2100, sample.Fans[0].Rpm);
    }

    [Fact]
    public void Merge_MultipleVendorGpuFans_UseShortAdapterNames()
    {
        var nvapi = new SensorProbeResult(null, null,
        [
            new GpuSensorReading(0x10DE, "NVIDIA GeForce RTX 5080", 47, 2100),
            new GpuSensorReading(0x10DE, "NVIDIA GeForce RTX 3060", 41, 1500)
        ], [], null, []);

        var sample = HardwareSensorMerger.Merge([Result("NVAPI", nvapi)]);

        Assert.Equal(2, sample.Fans.Count);
        Assert.Equal("RTX 5080", sample.Fans[0].Name);
        Assert.Equal("RTX 3060", sample.Fans[1].Name);
    }

    [Fact]
    public void Merge_DriveTemperatures_FlowThrough_AndCountAsReadings()
    {
        var storage = new SensorProbeResult(null, null, [], [], null,
            [new DriveTemperatureSample("C:", 38), new DriveTemperatureSample("D:", 41)]);

        var sample = HardwareSensorMerger.Merge([Result("Storage", storage)]);

        Assert.True(sample.HasAnyReading);
        Assert.Equal(2, sample.DriveTemperatures.Count);
        Assert.Equal("Storage", sample.SourceName);
    }

    [Fact]
    public void Merge_FanMaxRpm_FallsBackToVendorGpuRating()
    {
        var levelZero = new SensorProbeResult(null, null,
            [new GpuSensorReading(0x8086, "Intel GPU", null, 1600) { FanMaxRpm = 4000 }], [], null, []);

        var sample = HardwareSensorMerger.Merge([Result("Intel Level Zero", levelZero)]);

        Assert.Equal(4000, sample.FanMaxRpm);
        Assert.Single(sample.Fans);
    }

    [Fact]
    public void Merge_SourceName_ListsContributingProviders()
    {
        var lenovo = new SensorProbeResult(61, null, [], [], null, []);
        var nvapi = new SensorProbeResult(null, null,
            [new GpuSensorReading(0x10DE, "NVIDIA GeForce RTX 5080", 47, null)], [], null, []);

        var sample = HardwareSensorMerger.Merge([Result("Lenovo GameZone", lenovo), Result("NVAPI", nvapi)]);

        Assert.Equal("Lenovo GameZone + NVAPI", sample.SourceName);
    }
}
