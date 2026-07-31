using System.Runtime.InteropServices;

namespace WinMoe.Services;

/// <summary>
/// Reads ACPI thermal-zone temperatures through the PDH counter
/// "\Thermal Zone Information(*)\Temperature" (values in Kelvin). This is the
/// only brand-neutral CPU/board temperature source Windows exposes to standard
/// users; many machines publish no zones, in which case the probe disables
/// itself. Uses the English counter API so localized Windows builds work.
/// </summary>
public sealed class ThermalZoneSensorProbe : ISensorProbe
{
    private const string CounterPath = @"\Thermal Zone Information(*)\Temperature";

    private const int PdhMoreData = unchecked((int)0x800007D2);
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhCstatusValidData = 0x00000000;
    private const uint PdhCstatusNewData = 0x00000001;

    private bool _initialized;
    private bool _available;
    private IntPtr _query;
    private IntPtr _counter;

    public string Name => "ACPI Thermal Zone";

    public SensorProbeResult Capture()
    {
        if (!EnsureInitialized())
        {
            return SensorProbeResult.Empty;
        }

        try
        {
            if (PdhCollectQueryData(_query) != 0)
            {
                return SensorProbeResult.Empty;
            }

            var maxKelvin = ReadMaxKelvin();
            if (maxKelvin is null)
            {
                return SensorProbeResult.Empty;
            }

            var celsius = KelvinToCelsius(maxKelvin.Value);
            return celsius is > 0 and < 130
                ? new SensorProbeResult(celsius, null, [], [], null, [])
                : SensorProbeResult.Empty;
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
        {
            return SensorProbeResult.Empty;
        }
    }

    public static double KelvinToCelsius(double kelvin) => kelvin - 273.15;

    private bool EnsureInitialized()
    {
        if (_initialized)
        {
            return _available;
        }

        _initialized = true;
        if (PdhOpenQueryW(IntPtr.Zero, IntPtr.Zero, out _query) == 0 &&
            PdhAddEnglishCounterW(_query, CounterPath, IntPtr.Zero, out _counter) == 0)
        {
            // Prime the counter; the first collect has no data by design.
            PdhCollectQueryData(_query);
            _available = true;
        }

        return _available;
    }

    private double? ReadMaxKelvin()
    {
        var bufferSize = 0;
        var status = PdhGetFormattedCounterArrayW(_counter, PdhFmtDouble, ref bufferSize, out _, IntPtr.Zero);
        if (status != PdhMoreData || bufferSize <= 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            status = PdhGetFormattedCounterArrayW(_counter, PdhFmtDouble, ref bufferSize, out var itemCount, buffer);
            if (status != 0 || itemCount <= 0)
            {
                return null;
            }

            // PDH_FMT_COUNTERVALUE_ITEM_DOUBLE: szName(ptr) + CStatus(4) + double.
            // Alignment differs by process bitness: x64 pads to 24 bytes per
            // item, x86 packs to 16.
            var cstatusOffset = IntPtr.Size;
            var valueOffset = IntPtr.Size == 8 ? 16 : 8;
            var itemSize = IntPtr.Size == 8 ? 24 : 16;
            double? max = null;
            for (var i = 0; i < itemCount; i++)
            {
                var item = buffer + i * itemSize;
                var cStatus = Marshal.ReadInt32(item, cstatusOffset);
                if (cStatus != PdhCstatusValidData && cStatus != PdhCstatusNewData)
                {
                    continue;
                }

                var value = Marshal.ReadInt64(item, valueOffset);
                var kelvin = BitConverter.Int64BitsToDouble(value);
                if (kelvin is > 200 and < 450)
                {
                    max = max is null ? kelvin : Math.Max(max.Value, kelvin);
                }
            }

            return max;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhOpenQueryW(IntPtr dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhAddEnglishCounterW(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern int PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref int bufferSize, out int itemCount, IntPtr itemBuffer);
}
