namespace WinMoe.Models;

/// <summary>One fan reading from any sensor interface.</summary>
public sealed record FanSensorSample(string Name, int Rpm);

/// <summary>
/// Temperature/fan reading for one GPU from a vendor API (NVAPI for NVIDIA,
/// ADL for AMD). <see cref="VendorId"/> is the PCI vendor ID so readings can
/// be matched to DXGI adapters regardless of adapter naming.
/// </summary>
public sealed record GpuSensorReading(
    uint VendorId,
    string AdapterName,
    double? TemperatureCelsius,
    int? FanRpm)
{
    /// <summary>Rated max RPM for this GPU's fan when the vendor API reports it.</summary>
    public int? FanMaxRpm { get; init; }
}

/// <summary>One physical drive's temperature (universal storage query).</summary>
public sealed record DriveTemperatureSample(string DriveName, double TemperatureCelsius);

/// <summary>
/// Optional hardware sensor readings. Every field is nullable/empty because
/// availability depends on the machine: vendor WMI interfaces, GPU vendor APIs
/// (NVAPI/ADL), ACPI thermal zones and storage temperature properties each
/// cover different hardware. WinMoe merges whatever is present and shows an
/// honest unavailable state for the rest — never fabricated values.
/// </summary>
public sealed record HardwareSensorSample(
    double? CpuTemperatureCelsius,
    double? GpuTemperatureCelsius,
    IReadOnlyList<FanSensorSample> Fans,
    int? FanMaxRpm,
    string SourceName)
{
    /// <summary>Per-GPU readings from vendor APIs (NVIDIA/AMD), independent of brand.</summary>
    public IReadOnlyList<GpuSensorReading> GpuReadings { get; init; } = [];

    /// <summary>Physical drive temperatures when the storage stack reports them.</summary>
    public IReadOnlyList<DriveTemperatureSample> DriveTemperatures { get; init; } = [];

    public static HardwareSensorSample Unavailable { get; } = new(
        null,
        null,
        [],
        null,
        string.Empty);

    public bool HasAnyReading =>
        CpuTemperatureCelsius.HasValue ||
        GpuTemperatureCelsius.HasValue ||
        Fans.Count > 0 ||
        GpuReadings.Count > 0 ||
        DriveTemperatures.Count > 0;
}
