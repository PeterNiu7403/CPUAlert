using System.Runtime.InteropServices;
using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// Reads NVIDIA GPU temperature and fan RPM through NVAPI (nvapi64.dll), the
/// official user-mode interface installed with every NVIDIA display driver.
/// Works as a standard user with no extra kernel driver, on any brand of
/// desktop or laptop. Disables itself when no NVIDIA GPU/driver is present.
/// </summary>
public sealed class NvidiaGpuSensorProbe : ISensorProbe
{
    public const uint NvidiaPciVendorId = 0x10DE;

    // NVAPI entry IDs (stable ABI, published in the NVAPI SDK headers).
    private const uint InitializeId = 0x0150E828;
    private const uint EnumPhysicalGpusId = 0xE5AC921F;
    private const uint GetFullNameId = 0xCEEE8E9F;
    private const uint GetThermalSettingsId = 0xE3640A56;
    private const uint GetTachReadingId = 0x5F608315;

    private const int MaxPhysicalGpus = 64;
    private const int ShortStringMax = 64;
    private const int StatusOk = 0;

    // The driver ships both builds of the DLL; pick by process bitness
    // (WinMoe runs x86 by default → SysWOW64\nvapi.dll on 64-bit Windows).
    private static readonly string LibraryName = Environment.Is64BitProcess ? "nvapi64.dll" : "nvapi.dll";

    // NV_GPU_THERMAL_SETTINGS_V2: version + count + sensor[3] (20 bytes each).
    public const int ThermalSensorsPerGpu = 3;
    public const int ThermalSettingsSize = 8 + ThermalSensorsPerGpu * 20;
    public const uint ThermalSettingsV2Version = (uint)ThermalSettingsSize | (2u << 16);

    private readonly object _sync = new();
    private bool _initialized;
    private bool _available;
    private IntPtr _library;

    private NvApiEnumPhysicalGpus? _enumPhysicalGpus;
    private NvApiGetFullName? _getFullName;
    private NvApiGetThermalSettings? _getThermalSettings;
    private NvApiGetTachReading? _getTachReading;

    public string Name => "NVAPI";

    public SensorProbeResult Capture()
    {
        lock (_sync)
        {
            if (!EnsureInitialized())
            {
                return SensorProbeResult.Empty;
            }

            var handles = Marshal.AllocHGlobal(MaxPhysicalGpus * IntPtr.Size);
            try
            {
                if (_enumPhysicalGpus!(handles, out var count) != StatusOk || count <= 0)
                {
                    return SensorProbeResult.Empty;
                }

                count = Math.Min(count, MaxPhysicalGpus);
                var readings = new List<GpuSensorReading>(count);
                for (var index = 0; index < count; index++)
                {
                    var handle = Marshal.ReadIntPtr(handles, index * IntPtr.Size);
                    if (handle == IntPtr.Zero)
                    {
                        continue;
                    }

                    var name = ReadName(handle);
                    var temperature = ReadTemperature(handle);
                    var fanRpm = ReadFanRpm(handle);
                    if (temperature is null && fanRpm is null)
                    {
                        continue;
                    }

                    readings.Add(new GpuSensorReading(NvidiaPciVendorId, name, temperature, fanRpm));
                }

                return readings.Count == 0
                    ? SensorProbeResult.Empty
                    : new SensorProbeResult(null, null, readings, [], null, []);
            }
            catch (Exception ex) when (ex is AccessViolationException or SEHException or ExternalException)
            {
                // A driver hiccup must not poison the sensor chain; try again next tick.
                return SensorProbeResult.Empty;
            }
            finally
            {
                Marshal.FreeHGlobal(handles);
            }
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
            _library = LoadLibrary(LibraryName);
            if (_library == IntPtr.Zero)
            {
                return false;
            }

            var queryInterface = GetProcAddress(_library, "nvapi_QueryInterface");
            if (queryInterface == IntPtr.Zero)
            {
                return false;
            }

            var initialize = Resolve<NvApiInitialize>(queryInterface, InitializeId);
            _enumPhysicalGpus = Resolve<NvApiEnumPhysicalGpus>(queryInterface, EnumPhysicalGpusId);
            _getFullName = Resolve<NvApiGetFullName>(queryInterface, GetFullNameId);
            _getThermalSettings = Resolve<NvApiGetThermalSettings>(queryInterface, GetThermalSettingsId);
            _getTachReading = Resolve<NvApiGetTachReading>(queryInterface, GetTachReadingId);

            _available = initialize is not null &&
                _enumPhysicalGpus is not null &&
                _getThermalSettings is not null &&
                initialize() == StatusOk;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            _available = false;
        }

        return _available;
    }

    private static T? Resolve<T>(IntPtr queryInterface, uint functionId) where T : Delegate
    {
        // nvapi_QueryInterface(id) is itself a Cdecl function returning the entry pointer.
        var query = Marshal.GetDelegateForFunctionPointer<NvApiQueryInterface>(queryInterface);
        var pointer = query(functionId);
        return pointer == IntPtr.Zero
            ? null
            : Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }

    private string ReadName(IntPtr handle)
    {
        if (_getFullName is null)
        {
            return "NVIDIA GPU";
        }

        var buffer = Marshal.AllocHGlobal(ShortStringMax);
        try
        {
            if (_getFullName(handle, buffer) != StatusOk)
            {
                return "NVIDIA GPU";
            }

            var name = Marshal.PtrToStringAnsi(buffer);
            return string.IsNullOrWhiteSpace(name) ? "NVIDIA GPU" : name.Trim();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private double? ReadTemperature(IntPtr handle)
    {
        var settings = new NvGpuThermalSettings
        {
            Version = ThermalSettingsV2Version,
            Sensors = new NvThermalSensor[ThermalSensorsPerGpu]
        };
        if (_getThermalSettings!(handle, 0, ref settings) != StatusOk || settings.Count == 0)
        {
            return null;
        }

        // Sensor 0 is the GPU core. Bogus values (0xFF-filled or zero) are rejected.
        var temp = settings.Sensors[0].CurrentTemp;
        return temp is > 0 and < 150 ? temp : null;
    }

    private int? ReadFanRpm(IntPtr handle)
    {
        if (_getTachReading is null)
        {
            return null;
        }

        var status = _getTachReading(handle, out var rpm);
        // Many laptop GPUs report NOT_SUPPORTED here; that is a normal outcome.
        return status == StatusOk && rpm is > 0 and < 20000 ? rpm : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NvThermalSensor
    {
        public int Controller;
        public uint DefaultMinTemp;
        public uint DefaultMaxTemp;
        public uint CurrentTemp;
        public int Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NvGpuThermalSettings
    {
        public uint Version;
        public uint Count;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = ThermalSensorsPerGpu)]
        public NvThermalSensor[] Sensors;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr NvApiQueryInterface(uint functionId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvApiInitialize();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvApiEnumPhysicalGpus(IntPtr handles, out int count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvApiGetFullName(IntPtr handle, IntPtr nameBuffer);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvApiGetThermalSettings(IntPtr handle, uint sensorIndex, ref NvGpuThermalSettings settings);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvApiGetTachReading(IntPtr handle, out int rpm);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
}
