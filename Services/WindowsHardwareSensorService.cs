using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// Hardware sensor facade: runs the sensor probe chain and merges whatever is
/// available on the current machine — brand interfaces (Lenovo GameZone),
/// vendor GPU APIs (NVAPI for NVIDIA, ADL for AMD), ACPI thermal zones and
/// storage temperature queries. Every source works as a standard user without
/// extra kernel drivers; machines expose whichever subset they support.
/// </summary>
public sealed class WindowsHardwareSensorService : IHardwareSensorService
{
    private readonly object _sync = new();
    private readonly IReadOnlyList<ISensorProbe> _probes;

    public WindowsHardwareSensorService()
        : this(CreateDefaultProbes())
    {
    }

    public WindowsHardwareSensorService(IReadOnlyList<ISensorProbe> probes)
    {
        _probes = probes;
    }

    public static IReadOnlyList<ISensorProbe> CreateDefaultProbes()
    {
        return
        [
            new LenovoGameZoneSensorProbe(),
            new DellDcimSensorProbe(),
            new NvidiaGpuSensorProbe(),
            new AmdGpuSensorProbe(),
            new IntelGpuSensorProbe(),
            new ThermalZoneSensorProbe(),
            new StorageTemperatureSensorProbe()
        ];
    }

    public HardwareSensorSample Capture()
    {
        lock (_sync)
        {
            var results = new List<KeyValuePair<string, SensorProbeResult>>(_probes.Count);
            foreach (var probe in _probes)
            {
                try
                {
                    var result = probe.Capture();
                    if (result.HasAnyReading)
                    {
                        results.Add(new KeyValuePair<string, SensorProbeResult>(probe.Name, result));
                    }
                }
                catch
                {
                    // A broken provider must never take down the rest of the chain.
                }
            }

            return HardwareSensorMerger.Merge(results);
        }
    }
}
