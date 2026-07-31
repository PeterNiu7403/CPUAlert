using System.Text.Json.Serialization;

namespace WinMoe.Models;

public sealed record SystemTelemetrySnapshot(
    DateTimeOffset CapturedAt,
    double CpuUsagePercent,
    double MemoryUsagePercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    double DiskUsagePercent,
    long DiskUsedBytes,
    long DiskTotalBytes,
    double NetworkReceivedBytesPerSecond,
    double NetworkSentBytesPerSecond,
    string GpuStatus,
    IReadOnlyList<ProcessTelemetry> TopProcesses)
{
    public string NetworkInterfaceName { get; init; } = "network";

    public string NetworkIPv4Address { get; init; } = "unavailable";

    public double? BatteryChargePercent { get; init; }

    public string BatteryStatusText { get; init; } = "unavailable";

    public string BatteryHealthText { get; init; } = "Unavailable";

    public int? BatteryEstimatedSecondsRemaining { get; init; }

    public bool HasBattery { get; init; }

    /// <summary>Battery design capacity from the firmware (mWh); null when the probe fails.</summary>
    public long? BatteryDesignCapacityMwh { get; init; }

    /// <summary>Battery full-charge capacity (mWh); null when unavailable.</summary>
    public long? BatteryFullChargeCapacityMwh { get; init; }

    /// <summary>Reported charge cycles; null when unreported.</summary>
    public int? BatteryCycleCount { get; init; }

    /// <summary>Signed charge(+)/discharge(-) power in mW; null when idle or unavailable.</summary>
    public int? BatteryRateMw { get; init; }

    /// <summary>CPU package temperature when the platform exposes one (null = unavailable).</summary>
    public double? CpuTemperatureCelsius { get; init; }

    /// <summary>Discrete GPU temperature when the platform exposes one (null = unavailable).</summary>
    public double? GpuTemperatureCelsius { get; init; }

    public IReadOnlyList<FanSensorSample> Fans { get; init; } = [];

    public int? FanMaxRpm { get; init; }

    /// <summary>Name of the sensor provider, e.g. "Lenovo GameZone"; empty when none.</summary>
    public string SensorSource { get; init; } = string.Empty;

    /// <summary>Per-adapter GPU utilization (dGPU/iGPU split); empty when unavailable.</summary>
    public IReadOnlyList<GpuAdapterTelemetry> GpuAdapters { get; init; } = [];

    /// <summary>Number of physical disks backing the fixed volumes (0 = unknown).</summary>
    public int PhysicalDiskCount { get; init; }

    /// <summary>Number of ready fixed volumes aggregated into the disk totals.</summary>
    public int DiskVolumeCount { get; init; }

    /// <summary>Sum of total bytes across all ready fixed volumes.</summary>
    public long AllDisksTotalBytes { get; init; }

    /// <summary>Sum of free bytes across all ready fixed volumes.</summary>
    public long AllDisksFreeBytes { get; init; }

    /// <summary>Per-volume capacity for every ready fixed volume (partition view).</summary>
    public IReadOnlyList<DiskVolumeTelemetry> Volumes { get; init; } = [];

    [JsonIgnore]
    public string TimestampText => CapturedAt.ToLocalTime().ToString("HH:mm:ss");

    [JsonIgnore]
    public string CpuText => $"{CpuUsagePercent:0.0}%";

    [JsonIgnore]
    public string MemoryText => $"{MemoryUsagePercent:0.0}%";

    [JsonIgnore]
    public string DiskText => $"{DiskUsagePercent:0.0}%";

    public static SystemTelemetrySnapshot Empty(DateTimeOffset capturedAt)
    {
        return new SystemTelemetrySnapshot(
            capturedAt,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            "Unavailable",
            []);
    }
}
