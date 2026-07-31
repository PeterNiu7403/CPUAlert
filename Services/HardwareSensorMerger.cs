using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// Merges the readings of every available sensor probe into one sample.
/// Priority: brand-specific interfaces (Lenovo) first for CPU temperature and
/// chassis fans, vendor GPU APIs (NVAPI/ADL) for per-GPU values, ACPI thermal
/// zones as the CPU fallback, storage queries for drive temperatures.
/// </summary>
public static class HardwareSensorMerger
{
    public static HardwareSensorSample Merge(IReadOnlyList<KeyValuePair<string, SensorProbeResult>> results)
    {
        double? cpuTemperature = null;
        double? platformGpuTemperature = null;
        int? fanMaxRpm = null;
        var fans = new List<FanSensorSample>();
        var gpuReadings = new List<GpuSensorReading>();
        var driveTemperatures = new List<DriveTemperatureSample>();
        var sources = new List<string>();

        foreach (var (name, result) in results)
        {
            if (!result.HasAnyReading)
            {
                continue;
            }

            sources.Add(name);
            cpuTemperature ??= result.CpuTemperatureCelsius;
            platformGpuTemperature ??= result.GpuTemperatureCelsius;
            fanMaxRpm ??= result.FanMaxRpm;
            fans.AddRange(result.Fans);
            gpuReadings.AddRange(result.GpuReadings);
            driveTemperatures.AddRange(result.DriveTemperatures);
        }

        // Vendor GPU APIs may also know the fan's rated max (used for load %).
        fanMaxRpm ??= gpuReadings
            .Select(reading => reading.FanMaxRpm)
            .FirstOrDefault(max => max is > 0);

        AppendVendorGpuFans(fans, gpuReadings);

        // Aggregate GPU temperature: hottest vendor-API reading wins; fall back
        // to the platform scalar (e.g. Lenovo GameZone dGPU sensor).
        var aggregateGpuTemperature = gpuReadings
            .Select(reading => reading.TemperatureCelsius)
            .Where(temperature => temperature.HasValue)
            .Select(temperature => temperature!.Value)
            .DefaultIfEmpty()
            .Max() is { } max and > 0
                ? max
                : platformGpuTemperature;

        if (cpuTemperature is null &&
            aggregateGpuTemperature is null &&
            fans.Count == 0 &&
            gpuReadings.Count == 0 &&
            driveTemperatures.Count == 0)
        {
            return HardwareSensorSample.Unavailable;
        }

        return new HardwareSensorSample(
            cpuTemperature,
            aggregateGpuTemperature,
            fans,
            fanMaxRpm,
            string.Join(" + ", sources))
        {
            GpuReadings = gpuReadings,
            DriveTemperatures = driveTemperatures
        };
    }

    /// <summary>
    /// GPU vendor APIs report per-card fan RPM. Surface those as fan rows unless
    /// a platform interface already reports a GPU fan (same physical fan).
    /// </summary>
    private static void AppendVendorGpuFans(List<FanSensorSample> fans, List<GpuSensorReading> gpuReadings)
    {
        var platformHasGpuFan = fans.Any(fan => fan.Name.Contains("gpu", StringComparison.OrdinalIgnoreCase));
        if (platformHasGpuFan)
        {
            return;
        }

        var vendorFans = gpuReadings
            .Where(reading => reading.FanRpm is > 0)
            .ToArray();
        foreach (var reading in vendorFans)
        {
            var name = vendorFans.Length == 1
                ? "GPU"
                : GpuAdapterTelemetry.Shorten(reading.AdapterName);
            fans.Add(new FanSensorSample(name, reading.FanRpm!.Value));
        }
    }
}
