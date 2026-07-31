using System.Management;
using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// Reads CPU/GPU temperatures and fan speeds from the Lenovo GameZone WMI
/// interface (root\WMI LENOVO_OTHER_METHOD.GetFeatureValue), the same channel
/// Lenovo Vantage / LenovoLegionToolkit use on Legion laptops. On any machine
/// without that interface the probe disables itself — no driver, no admin, no
/// fabricated values.
/// </summary>
public sealed class LenovoGameZoneSensorProbe : ISensorProbe
{
    // Capability IDs from Lenovo's GameZone interface (public knowledge via
    // the open-source LenovoLegionToolkit project, CapabilityID enum).
    private const int CpuCurrentTemperatureId = 0x05040000;
    private const int GpuCurrentTemperatureId = 0x05050000;
    private const int CpuCurrentFanSpeedId = 0x04030001;
    private const int GpuCurrentFanSpeedId = 0x04030002;

    private ManagementObject? _gameZoneInstance;
    private bool _probeAttempted;
    private bool _interfaceAvailable;

    public string Name => "Lenovo GameZone";

    public SensorProbeResult Capture()
    {
        if (!EnsureInterface())
        {
            return SensorProbeResult.Empty;
        }

        try
        {
            var cpuTemp = ReadTemperature(CpuCurrentTemperatureId);
            var gpuTemp = ReadTemperature(GpuCurrentTemperatureId);
            var fans = ReadFans();
            var maxRpm = ReadMaxFanRpm();

            return new SensorProbeResult(cpuTemp, gpuTemp, [], fans, maxRpm, []);
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return SensorProbeResult.Empty;
        }
    }

    private bool EnsureInterface()
    {
        if (_probeAttempted)
        {
            return _interfaceAvailable;
        }

        _probeAttempted = true;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\WMI",
                "SELECT * FROM LENOVO_OTHER_METHOD");
            using var results = searcher.Get();
            _gameZoneInstance = results.Cast<ManagementObject>().FirstOrDefault();
            _interfaceAvailable = _gameZoneInstance is not null;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            _interfaceAvailable = false;
        }

        return _interfaceAvailable;
    }

    private int? InvokeGetFeatureValue(int featureId)
    {
        try
        {
            if (_gameZoneInstance is null)
            {
                return null;
            }

            // Named in/out parameters: the positional InvokeMethod(name, object[])
            // overload returns null for this WMI provider.
            using var inParams = _gameZoneInstance.GetMethodParameters("GetFeatureValue");
            inParams["IDs"] = featureId;
            using var output = _gameZoneInstance.InvokeMethod("GetFeatureValue", inParams, null);
            if (output is null)
            {
                return null;
            }

            var raw = output["Value"];
            return raw is null ? null : Convert.ToInt32(raw);
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or InvalidCastException)
        {
            // The interface may vanish (driver update, sleep/resume); re-probe next time.
            _probeAttempted = false;
            _gameZoneInstance?.Dispose();
            _gameZoneInstance = null;
            return null;
        }
    }

    private double? ReadTemperature(int featureId)
    {
        var value = InvokeGetFeatureValue(featureId);
        // Lenovo returns 0 when the sensor is not routed on this model.
        return value is > 0 and < 130 ? value.Value : null;
    }

    private IReadOnlyList<FanSensorSample> ReadFans()
    {
        var fans = new List<FanSensorSample>(2);
        var cpuFan = InvokeGetFeatureValue(CpuCurrentFanSpeedId);
        if (cpuFan is > 0 and < 20000)
        {
            fans.Add(new FanSensorSample("CPU", cpuFan.Value));
        }

        var gpuFan = InvokeGetFeatureValue(GpuCurrentFanSpeedId);
        if (gpuFan is > 0 and < 20000)
        {
            fans.Add(new FanSensorSample("GPU", gpuFan.Value));
        }

        return fans;
    }

    private static int? ReadMaxFanRpm()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\WMI",
                "SELECT CurrentFanMaxSpeed FROM LENOVO_FAN_TABLE_DATA");
            using var results = searcher.Get();
            var max = 0;
            foreach (ManagementObject row in results)
            {
                try
                {
                    var value = Convert.ToInt32(row["CurrentFanMaxSpeed"]);
                    max = Math.Max(max, value);
                }
                catch (Exception ex) when (ex is ManagementException or InvalidCastException or FormatException)
                {
                }
                finally
                {
                    row.Dispose();
                }
            }

            return max > 0 ? max : null;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }
}
