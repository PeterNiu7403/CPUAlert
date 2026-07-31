using WinMoe.Services;
using Xunit;
using Xunit.Abstractions;

namespace WinMoe.Tests;

/// <summary>
/// Live-machine probes for the hardware sensor and GPU adapter services.
/// They assert shape (non-crashing, sane ranges) and print readings so machine
/// runs can eyeball the actual values; on machines without the vendor sensor
/// interface the "unavailable" contract is asserted instead.
/// </summary>
public sealed class HardwareSensorProbeTests
{
    private readonly ITestOutputHelper _output;

    public HardwareSensorProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void HardwareSensorService_Capture_DoesNotThrow_AndReportsSaneRanges()
    {
        var service = new WindowsHardwareSensorService();

        var sample = service.Capture();

        _output.WriteLine($"source='{sample.SourceName}' cpu={sample.CpuTemperatureCelsius}C gpu={sample.GpuTemperatureCelsius}C " +
                          $"fans=[{string.Join(", ", sample.Fans.Select(f => $"{f.Name}:{f.Rpm}rpm"))}] maxRpm={sample.FanMaxRpm} " +
                          $"gpuReadings=[{string.Join(", ", sample.GpuReadings.Select(g => $"{g.AdapterName}:{g.TemperatureCelsius}C/{g.FanRpm}rpm"))}] " +
                          $"drives=[{string.Join(", ", sample.DriveTemperatures.Select(d => $"{d.DriveName}:{d.TemperatureCelsius}C"))}]");

        Assert.InRange(sample.CpuTemperatureCelsius ?? 25, 1, 130);
        Assert.InRange(sample.GpuTemperatureCelsius ?? 25, 1, 130);
        foreach (var fan in sample.Fans)
        {
            Assert.InRange(fan.Rpm, 1, 20000);
            Assert.False(string.IsNullOrWhiteSpace(fan.Name));
        }

        if (sample.HasAnyReading)
        {
            Assert.False(string.IsNullOrWhiteSpace(sample.SourceName));
        }
    }

    [Fact]
    public void GpuAdapterService_CaptureAdapters_ReportsNamesAndClampedUsage()
    {
        var service = new WindowsGpuAdapterService();

        var adapters = service.CaptureAdapters();

        _output.WriteLine($"adapters: {string.Join(" | ", adapters.Select(a => $"{a.Name} [{a.Kind}] 3D={a.Engine3DPercent:0.0}%"))}");
        // GPU-less CI runners legitimately return zero adapters; only validate
        // the contract when hardware is present.
        foreach (var adapter in adapters)
        {
            Assert.False(string.IsNullOrWhiteSpace(adapter.Name));
            Assert.InRange(adapter.Engine3DPercent, 0, 100);
        }
    }

    [Fact]
    public async Task SystemTelemetryService_Capture_FillsExtendedFields()
    {
        var service = new WindowsSystemTelemetryService();

        var snapshot = await service.CaptureAsync();

        _output.WriteLine($"cpuTemp={snapshot.CpuTemperatureCelsius} gpuTemp={snapshot.GpuTemperatureCelsius} " +
                          $"fans={snapshot.Fans.Count} physicalDisks={snapshot.PhysicalDiskCount} " +
                          $"volumes={snapshot.DiskVolumeCount} allTotal={snapshot.AllDisksTotalBytes} allFree={snapshot.AllDisksFreeBytes} " +
                          $"gpuAdapters={snapshot.GpuAdapters.Count} sensorSource='{snapshot.SensorSource}' " +
                          $"adapters=[{string.Join(", ", snapshot.GpuAdapters.Select(a => $"{a.Name} vendor={a.VendorId:X4} temp={a.TemperatureCelsius}"))}]");

        Assert.True(snapshot.DiskVolumeCount >= 1);
        Assert.True(snapshot.AllDisksTotalBytes >= snapshot.DiskTotalBytes);
        Assert.True(snapshot.AllDisksFreeBytes >= 0);
        Assert.True(snapshot.PhysicalDiskCount >= 1);
    }
}
