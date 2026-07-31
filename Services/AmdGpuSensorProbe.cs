using System.Runtime.InteropServices;
using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// Reads AMD GPU temperature and fan RPM through ADL (atiadlxx.dll), the AMD
/// display driver's user-mode interface. Works as a standard user with no
/// extra kernel driver. Disables itself when no AMD GPU/driver is present.
/// </summary>
public sealed class AmdGpuSensorProbe : ISensorProbe
{
    public const uint AmdPciVendorId = 0x1002;

    private const int AdlOk = 0;
    private const int AdlTemperatureCore = 1;
    private const int FanSpeedTypeRpm = 2;

    // The driver ships both builds of the DLL; pick by process bitness
    // (WinMoe runs x86 by default → SysWOW64\atiadlxy.dll on 64-bit Windows).
    private static readonly string LibraryName = Environment.Is64BitProcess ? "atiadlxx.dll" : "atiadlxy.dll";

    // ADL AdapterInfo record layout (ADL_MAX_PATH = 256); read via offsets so
    // no full struct marshalling is needed.
    private const int AdapterInfoSizeOffset = 0;
    private const int AdapterInfoIndexOffset = 4;
    private const int AdapterInfoNameOffset = 280;
    private const int AdapterInfoMinStride = 1572;

    private readonly object _sync = new();
    private bool _initialized;
    private bool _available;
    private IntPtr _library;
    private IntPtr _context;

    private Adl2AdapterInfoGet? _adapterInfoGet;
    private Adl2OverdriveNTemperatureGet? _overdriveNTemperatureGet;
    private Adl2Overdrive6TemperatureGet? _overdrive6TemperatureGet;
    private Adl2OverdriveNFanSpeedGet? _overdriveNFanSpeedGet;
    private Adl2Overdrive5FanSpeedGet? _overdrive5FanSpeedGet;

    public string Name => "AMD ADL";

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
        if (_adapterInfoGet!(_context, out var info, out var count) != AdlOk ||
            info == IntPtr.Zero ||
            count <= 0)
        {
            return SensorProbeResult.Empty;
        }

        try
        {
            var readings = new List<GpuSensorReading>();
            var seenIndices = new HashSet<int>();
            var stride = Math.Max(Marshal.ReadInt32(info, AdapterInfoSizeOffset), AdapterInfoMinStride);
            for (var i = 0; i < count; i++)
            {
                var record = info + i * stride;
                var adapterIndex = Marshal.ReadInt32(record, AdapterInfoIndexOffset);
                // ADL lists one record per display output; dedupe by adapter index.
                if (!seenIndices.Add(adapterIndex))
                {
                    continue;
                }

                var temperature = ReadTemperature(adapterIndex);
                var fanRpm = ReadFanRpm(adapterIndex);
                if (temperature is null && fanRpm is null)
                {
                    continue;
                }

                readings.Add(new GpuSensorReading(
                    AmdPciVendorId,
                    ReadAnsi(record + AdapterInfoNameOffset, 256, "AMD GPU"),
                    temperature,
                    fanRpm));
            }

            return readings.Count == 0
                ? SensorProbeResult.Empty
                : new SensorProbeResult(null, null, readings, [], null, []);
        }
        finally
        {
            // The buffer was created by our own allocator callback.
            Marshal.FreeHGlobal(info);
        }
    }

    private double? ReadTemperature(int adapterIndex)
    {
        // OverdriveN covers GCN 3 (Fury) and newer; fall back to Overdrive6.
        if (_overdriveNTemperatureGet is not null)
        {
            var value = new AdlTemperature { Size = Marshal.SizeOf<AdlTemperature>() };
            if (_overdriveNTemperatureGet(_context, adapterIndex, AdlTemperatureCore, ref value) == AdlOk)
            {
                var celsius = value.Temperature / 1000.0;
                if (celsius is > 0 and < 150)
                {
                    return celsius;
                }
            }
        }

        if (_overdrive6TemperatureGet is not null &&
            _overdrive6TemperatureGet(_context, adapterIndex, out var millidegrees) == AdlOk)
        {
            var celsius = millidegrees / 1000.0;
            if (celsius is > 0 and < 150)
            {
                return celsius;
            }
        }

        return null;
    }

    private int? ReadFanRpm(int adapterIndex)
    {
        if (_overdriveNFanSpeedGet is not null)
        {
            var value = new AdlFanSpeedValue { Size = Marshal.SizeOf<AdlFanSpeedValue>() };
            if (_overdriveNFanSpeedGet(_context, adapterIndex, ref value) == AdlOk &&
                value.SpeedType == FanSpeedTypeRpm &&
                value.FanSpeed is > 0 and < 20000)
            {
                return value.FanSpeed;
            }
        }

        if (_overdrive5FanSpeedGet is not null)
        {
            var value = new AdlFanSpeedValue { Size = Marshal.SizeOf<AdlFanSpeedValue>() };
            if (_overdrive5FanSpeedGet(_context, adapterIndex, 0, ref value) == AdlOk &&
                value.SpeedType == FanSpeedTypeRpm &&
                value.FanSpeed is > 0 and < 20000)
            {
                return value.FanSpeed;
            }
        }

        return null;
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

            var create = GetDelegate<Adl2MainControlCreate>("ADL2_Main_Control_Create");
            _adapterInfoGet = GetDelegate<Adl2AdapterInfoGet>("ADL2_Adapter_AdapterInfo_Get");
            _overdriveNTemperatureGet = GetDelegate<Adl2OverdriveNTemperatureGet>("ADL2_OverdriveN_Temperature_Get");
            _overdrive6TemperatureGet = GetDelegate<Adl2Overdrive6TemperatureGet>("ADL2_Overdrive6_Temperature_Get");
            _overdriveNFanSpeedGet = GetDelegate<Adl2OverdriveNFanSpeedGet>("ADL2_OverdriveN_FanSpeed_Get");
            _overdrive5FanSpeedGet = GetDelegate<Adl2Overdrive5FanSpeedGet>("ADL2_Overdrive5_FanSpeed_Get");

            _available = create is not null &&
                _adapterInfoGet is not null &&
                create(MemoryAlloc, 1, out _context) == AdlOk &&
                _context != IntPtr.Zero;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            _available = false;
        }

        return _available;
    }

    private static IntPtr MemoryAlloc(int size) => Marshal.AllocHGlobal(size);

    private T? GetDelegate<T>(string entryPoint) where T : Delegate
    {
        var pointer = GetProcAddress(_library, entryPoint);
        return pointer == IntPtr.Zero
            ? null
            : Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }

    private static string ReadAnsi(IntPtr buffer, int maxLength, string fallback)
    {
        var value = Marshal.PtrToStringAnsi(buffer, maxLength);
        if (value is null)
        {
            return fallback;
        }

        var terminator = value.IndexOf('\0');
        if (terminator >= 0)
        {
            value = value[..terminator];
        }

        value = value.Trim();
        return value.Length == 0 ? fallback : value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AdlTemperature
    {
        public int Size;
        public int Temperature;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AdlFanSpeedValue
    {
        public int Size;
        public int SpeedType;
        public int FanSpeed;
        public int Flags;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr AdlMemoryAlloc(int size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Adl2MainControlCreate(AdlMemoryAlloc allocCallback, int enumConnectedAdapters, out IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Adl2AdapterInfoGet(IntPtr context, out IntPtr info, out int count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Adl2OverdriveNTemperatureGet(IntPtr context, int adapterIndex, int temperatureType, ref AdlTemperature temperature);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Adl2Overdrive6TemperatureGet(IntPtr context, int adapterIndex, out int millidegrees);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Adl2OverdriveNFanSpeedGet(IntPtr context, int adapterIndex, ref AdlFanSpeedValue fanSpeed);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Adl2Overdrive5FanSpeedGet(IntPtr context, int adapterIndex, int thermalControllerIndex, ref AdlFanSpeedValue fanSpeed);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
}
