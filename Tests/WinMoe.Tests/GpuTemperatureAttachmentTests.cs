using WinMoe.Models;
using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

/// <summary>GPU sensor-to-adapter matching by PCI vendor ID.</summary>
public sealed class GpuTemperatureAttachmentTests
{
    private static GpuAdapterTelemetry Adapter(string name, GpuAdapterKind kind, uint vendorId, double? temp = null) =>
        new(name, kind, 0, temp) { VendorId = vendorId };

    [Fact]
    public void Attach_VendorReading_MatchesAdapterByVendorId()
    {
        var adapters = new GpuAdapterTelemetry[]
        {
            Adapter("Intel(R) Graphics", GpuAdapterKind.Integrated, 0x8086),
            Adapter("NVIDIA GeForce RTX 5080 Laptop GPU", GpuAdapterKind.Discrete, 0x10DE)
        };
        var sensors = HardwareSensorSample.Unavailable with
        {
            GpuReadings = [new GpuSensorReading(0x10DE, "NVIDIA GeForce RTX 5080", 47, null)]
        };

        var result = GpuTemperatureAttachment.Attach(adapters, sensors);

        Assert.Null(result[0].TemperatureCelsius);
        Assert.Equal(47, result[1].TemperatureCelsius);
    }

    [Fact]
    public void Attach_PlatformScalar_OnlyFillsDiscreteAdapter()
    {
        var adapters = new GpuAdapterTelemetry[]
        {
            Adapter("Intel(R) Graphics", GpuAdapterKind.Integrated, 0x8086),
            Adapter("NVIDIA GeForce RTX 5080 Laptop GPU", GpuAdapterKind.Discrete, 0x10DE)
        };
        var sensors = new HardwareSensorSample(null, 44, [], null, "Lenovo GameZone");

        var result = GpuTemperatureAttachment.Attach(adapters, sensors);

        Assert.Null(result[0].TemperatureCelsius);
        Assert.Equal(44, result[1].TemperatureCelsius);
    }

    [Fact]
    public void Attach_VendorReading_TakesPriorityOverPlatformScalar()
    {
        var adapters = new GpuAdapterTelemetry[]
        {
            Adapter("NVIDIA GeForce RTX 5080 Laptop GPU", GpuAdapterKind.Discrete, 0x10DE)
        };
        var sensors = new HardwareSensorSample(null, 44, [], null, "Lenovo GameZone")
        {
            GpuReadings = [new GpuSensorReading(0x10DE, "NVIDIA GeForce RTX 5080", 47, null)]
        };

        var result = GpuTemperatureAttachment.Attach(adapters, sensors);

        Assert.Equal(47, result[0].TemperatureCelsius);
    }

    [Fact]
    public void Attach_SameVendorAdapters_ConsumeReadingsInOrder()
    {
        var adapters = new GpuAdapterTelemetry[]
        {
            Adapter("NVIDIA GeForce RTX 5080", GpuAdapterKind.Discrete, 0x10DE),
            Adapter("NVIDIA GeForce RTX 3060", GpuAdapterKind.Discrete, 0x10DE)
        };
        var sensors = HardwareSensorSample.Unavailable with
        {
            GpuReadings =
            [
                new GpuSensorReading(0x10DE, "NVIDIA GeForce RTX 5080", 47, null),
                new GpuSensorReading(0x10DE, "NVIDIA GeForce RTX 3060", 41, null)
            ]
        };

        var result = GpuTemperatureAttachment.Attach(adapters, sensors);

        Assert.Equal(47, result[0].TemperatureCelsius);
        Assert.Equal(41, result[1].TemperatureCelsius);
    }

    [Fact]
    public void Attach_ExistingTemperature_IsPreserved()
    {
        var adapters = new GpuAdapterTelemetry[]
        {
            Adapter("NVIDIA GeForce RTX 5080", GpuAdapterKind.Discrete, 0x10DE, temp: 52)
        };
        var sensors = HardwareSensorSample.Unavailable with
        {
            GpuReadings = [new GpuSensorReading(0x10DE, "NVIDIA GeForce RTX 5080", 47, null)]
        };

        var result = GpuTemperatureAttachment.Attach(adapters, sensors);

        Assert.Equal(52, result[0].TemperatureCelsius);
    }

    [Fact]
    public void Attach_EmptyAdapters_ReturnsEmpty()
    {
        var result = GpuTemperatureAttachment.Attach([], new HardwareSensorSample(null, 44, [], null, "Lenovo GameZone"));

        Assert.Empty(result);
    }
}
