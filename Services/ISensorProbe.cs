using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// One sensor provider's readings for a single tick. Every field is
/// nullable/empty — a provider only fills what its interface genuinely offers
/// on the current machine.
/// </summary>
public sealed record SensorProbeResult(
    double? CpuTemperatureCelsius,
    double? GpuTemperatureCelsius,
    IReadOnlyList<GpuSensorReading> GpuReadings,
    IReadOnlyList<FanSensorSample> Fans,
    int? FanMaxRpm,
    IReadOnlyList<DriveTemperatureSample> DriveTemperatures)
{
    public static SensorProbeResult Empty { get; } = new(null, null, [], [], null, []);

    public bool HasAnyReading =>
        CpuTemperatureCelsius.HasValue ||
        GpuTemperatureCelsius.HasValue ||
        GpuReadings.Count > 0 ||
        Fans.Count > 0 ||
        FanMaxRpm.HasValue ||
        DriveTemperatures.Count > 0;
}

/// <summary>
/// One hardware sensor source (vendor WMI, GPU vendor API, ACPI thermal zone,
/// storage temperature…). Implementations must detect missing interfaces fast,
/// stay disabled afterwards, and never throw from <see cref="Capture"/> — a
/// broken provider must never take down the rest of the chain.
/// </summary>
public interface ISensorProbe
{
    /// <summary>Display name used in the merged source attribution.</summary>
    string Name { get; }

    SensorProbeResult Capture();
}
