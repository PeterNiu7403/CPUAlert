using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// Matches sensor readings to DXGI adapters. Vendor-API readings (NVAPI/ADL)
/// carry the PCI vendor ID and are matched to adapters of the same vendor in
/// enumeration order; the platform scalar (e.g. Lenovo GameZone) reads the
/// discrete adapter — integrated GPUs share the CPU package and report no
/// separate die temperature.
/// </summary>
public static class GpuTemperatureAttachment
{
    public static IReadOnlyList<GpuAdapterTelemetry> Attach(
        IReadOnlyList<GpuAdapterTelemetry> adapters,
        HardwareSensorSample sensors)
    {
        if (adapters.Count == 0)
        {
            return adapters;
        }

        var temperaturesByVendor = sensors.GpuReadings
            .GroupBy(reading => reading.VendorId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(reading => reading.TemperatureCelsius).ToList());
        var consumedByVendor = new Dictionary<uint, int>();

        return adapters
            .Select(adapter => AttachOne(adapter, sensors, temperaturesByVendor, consumedByVendor))
            .ToArray();
    }

    private static GpuAdapterTelemetry AttachOne(
        GpuAdapterTelemetry adapter,
        HardwareSensorSample sensors,
        Dictionary<uint, List<double?>> temperaturesByVendor,
        Dictionary<uint, int> consumedByVendor)
    {
        if (adapter.TemperatureCelsius is not null)
        {
            return adapter;
        }

        if (temperaturesByVendor.TryGetValue(adapter.VendorId, out var vendorTemperatures))
        {
            consumedByVendor.TryGetValue(adapter.VendorId, out var index);
            consumedByVendor[adapter.VendorId] = index + 1;
            if (index < vendorTemperatures.Count && vendorTemperatures[index] is { } vendorTemperature)
            {
                return adapter with { TemperatureCelsius = vendorTemperature };
            }
        }

        if (adapter.Kind == GpuAdapterKind.Discrete && sensors.GpuTemperatureCelsius is { } platformTemperature)
        {
            return adapter with { TemperatureCelsius = platformTemperature };
        }

        return adapter;
    }
}
