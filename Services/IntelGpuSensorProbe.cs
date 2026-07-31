using System.Runtime.InteropServices;
using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// Reads Intel GPU temperatures and fan RPM through Intel's oneAPI Level Zero
/// Sysman API (ze_loader.dll, installed with every Intel graphics driver) —
/// the same user-mode channel GPU-Z uses for Intel. No admin, no extra kernel
/// driver. Discrete Arc cards expose die temps and fans; most integrated GPUs
/// expose no sensors, in which case the probe simply contributes nothing.
/// </summary>
public sealed class IntelGpuSensorProbe : ISensorProbe
{
    public const uint IntelPciVendorId = 0x8086;

    private const int StatusOk = 0;
    private const int FanSpeedUnitsRpm = 0;

    private readonly object _sync = new();
    private bool _initialized;
    private bool _available;

    private ZesDriverGet? _driverGet;
    private ZesDeviceGet? _deviceGet;
    private ZesDeviceEnumTemperatureSensors? _enumTemperatureSensors;
    private ZesTemperatureGetState? _temperatureGetState;
    private ZesDeviceEnumFans? _enumFans;
    private ZesFanGetProperties? _fanGetProperties;
    private ZesFanGetState? _fanGetState;

    public string Name => "Intel Level Zero";

    public SensorProbeResult Capture()
    {
        lock (_sync)
        {
            if (!EnsureInitialized())
            {
                return SensorProbeResult.Empty;
            }

            try
            {
                return CaptureCore();
            }
            catch (Exception ex) when (ex is AccessViolationException or SEHException or ExternalException)
            {
                return SensorProbeResult.Empty;
            }
        }
    }

    private SensorProbeResult CaptureCore()
    {
        var readings = new List<GpuSensorReading>();
        foreach (var driver in EnumerateDrivers())
        {
            foreach (var device in EnumerateHandles(_deviceGet!.Invoke, driver))
            {
                var temperature = ReadMaxTemperature(device);
                var (fanRpm, fanMaxRpm) = ReadFan(device);
                if (temperature is null && fanRpm is null)
                {
                    continue;
                }

                readings.Add(new GpuSensorReading(IntelPciVendorId, "Intel GPU", temperature, fanRpm)
                {
                    FanMaxRpm = fanMaxRpm
                });
            }
        }

        return readings.Count == 0
            ? SensorProbeResult.Empty
            : new SensorProbeResult(null, null, readings, [], null, []);
    }

    private double? ReadMaxTemperature(IntPtr device)
    {
        double? max = null;
        foreach (var sensor in EnumerateHandles(_enumTemperatureSensors!.Invoke, device))
        {
            if (_temperatureGetState!(sensor, out var celsius) == StatusOk && celsius is > 0 and < 130)
            {
                max = max is null ? celsius : Math.Max(max.Value, celsius);
            }
        }

        return max;
    }

    private (int? Rpm, int? MaxRpm) ReadFan(IntPtr device)
    {
        int? bestRpm = null;
        int? bestMax = null;
        foreach (var fan in EnumerateHandles(_enumFans!.Invoke, device))
        {
            if (_fanGetState!(fan, FanSpeedUnitsRpm, out var rpm) == StatusOk && rpm is > 0 and < 20000)
            {
                bestRpm = bestRpm is null ? rpm : Math.Max(bestRpm.Value, rpm);
            }

            if (_fanGetProperties is not null)
            {
                var properties = new ZesFanProperties { SType = ZesStructureTypeFanProperties };
                if (_fanGetProperties(fan, ref properties) == StatusOk && properties.MaxRpm is > 0 and < 20000)
                {
                    bestMax = bestMax is null ? properties.MaxRpm : Math.Max(bestMax.Value, properties.MaxRpm);
                }
            }
        }

        return (bestRpm, bestMax);
    }

    private delegate int HandleEnumerator(IntPtr parent, ref uint count, IntPtr handles);

    private IEnumerable<IntPtr> EnumerateDrivers()
    {
        uint count = 0;
        if (_driverGet!(ref count, IntPtr.Zero) != StatusOk || count == 0 || count > 8)
        {
            yield break;
        }

        var buffer = Marshal.AllocHGlobal((int)count * IntPtr.Size);
        try
        {
            if (_driverGet(ref count, buffer) != StatusOk)
            {
                yield break;
            }

            for (var index = 0; index < count; index++)
            {
                var handle = Marshal.ReadIntPtr(buffer, index * IntPtr.Size);
                if (handle != IntPtr.Zero)
                {
                    yield return handle;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IEnumerable<IntPtr> EnumerateHandles(HandleEnumerator enumerator, IntPtr parent)
    {
        uint count = 0;
        if (enumerator(parent, ref count, IntPtr.Zero) != StatusOk || count == 0 || count > 64)
        {
            yield break;
        }

        var buffer = Marshal.AllocHGlobal((int)count * IntPtr.Size);
        try
        {
            if (enumerator(parent, ref count, buffer) != StatusOk)
            {
                yield break;
            }

            for (var index = 0; index < count; index++)
            {
                var handle = Marshal.ReadIntPtr(buffer, index * IntPtr.Size);
                if (handle != IntPtr.Zero)
                {
                    yield return handle;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private bool EnsureInitialized()
    {
        if (_initialized)
        {
            return _available;
        }

        _initialized = true;
        try
        {
            // ze_loader.dll resolves per process bitness; absent on machines
            // without Intel graphics drivers → probe stays disabled.
            var library = LoadLibrary("ze_loader.dll");
            if (library == IntPtr.Zero)
            {
                return false;
            }

            var init = GetDelegate<ZesInit>(library, "zesInit");
            _driverGet = GetDelegate<ZesDriverGet>(library, "zesDriverGet");
            _deviceGet = GetDelegate<ZesDeviceGet>(library, "zesDeviceGet");
            _enumTemperatureSensors = GetDelegate<ZesDeviceEnumTemperatureSensors>(library, "zesDeviceEnumTemperatureSensors");
            _temperatureGetState = GetDelegate<ZesTemperatureGetState>(library, "zesTemperatureGetState");
            _enumFans = GetDelegate<ZesDeviceEnumFans>(library, "zesDeviceEnumFans");
            _fanGetProperties = GetDelegate<ZesFanGetProperties>(library, "zesFanGetProperties");
            _fanGetState = GetDelegate<ZesFanGetState>(library, "zesFanGetState");

            _available = init is not null &&
                _driverGet is not null &&
                _deviceGet is not null &&
                _enumTemperatureSensors is not null &&
                _temperatureGetState is not null &&
                _enumFans is not null &&
                _fanGetState is not null &&
                init(0) == StatusOk;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            _available = false;
        }

        return _available;
    }

    private static T? GetDelegate<T>(IntPtr library, string entryPoint) where T : Delegate
    {
        var pointer = GetProcAddress(library, entryPoint);
        return pointer == IntPtr.Zero
            ? null
            : Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }

    private const uint ZesStructureTypeFanProperties = 0x7;

    [StructLayout(LayoutKind.Sequential)]
    private struct ZesFanProperties
    {
        public uint SType;
        public IntPtr PNext;
        public uint OnSubdevice;
        public uint SubdeviceId;
        public uint CanControl;
        public uint SupportedModes;
        public uint SupportedUnits;
        public int MaxRpm;
        public int MaxPoints;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ZesInit(uint flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ZesDriverGet(ref uint count, IntPtr drivers);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ZesDeviceGet(IntPtr driver, ref uint count, IntPtr devices);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ZesDeviceEnumTemperatureSensors(IntPtr device, ref uint count, IntPtr sensors);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ZesTemperatureGetState(IntPtr sensor, out double celsius);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ZesDeviceEnumFans(IntPtr device, ref uint count, IntPtr fans);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ZesFanGetProperties(IntPtr fan, ref ZesFanProperties properties);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ZesFanGetState(IntPtr fan, int units, out int speed);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
}
