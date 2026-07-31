using System.Management;
using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// Reads fan speeds and temperature probes from Dell's DCIM WMI provider
/// (root\DCIM\SYSMAN, installed with Dell Command | Monitor on Latitude,
/// OptiPlex, Precision and similar business machines). On any machine without
/// the provider the probe disables itself — read-only, standard user.
/// </summary>
public sealed class DellDcimSensorProbe : ISensorProbe
{
    private const string NamespacePath = @"\\.\root\DCIM\SYSMAN";

    private bool _probeAttempted;
    private bool _interfaceAvailable;

    public string Name => "Dell DCIM";

    public SensorProbeResult Capture()
    {
        if (!EnsureInterface())
        {
            return SensorProbeResult.Empty;
        }

        try
        {
            var cpuTemperature = ReadCpuTemperature();
            var fans = ReadFans();
            return cpuTemperature is null && fans.Count == 0
                ? SensorProbeResult.Empty
                : new SensorProbeResult(cpuTemperature, null, [], fans, null, []);
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
            using var searcher = new ManagementObjectSearcher(NamespacePath, "SELECT ElementName FROM DCIM_Fan");
            using var results = searcher.Get();
            _interfaceAvailable = true;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            _interfaceAvailable = false;
        }

        return _interfaceAvailable;
    }

    private static double? ReadCpuTemperature()
    {
        double? cpuLabeled = null;
        double? anyProbe = null;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                NamespacePath,
                "SELECT ElementName, CurrentReading FROM DCIM_TemperatureProbe");
            using var rows = searcher.Get();
            foreach (ManagementObject row in rows)
            {
                try
                {
                    var value = Convert.ToDouble(row["CurrentReading"]);
                    if (value is <= 0 or >= 130)
                    {
                        continue;
                    }

                    var elementName = Convert.ToString(row["ElementName"]) ?? string.Empty;
                    anyProbe ??= value;
                    if (elementName.Contains("cpu", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuLabeled ??= value;
                    }
                }
                catch (Exception ex) when (ex is ManagementException or InvalidCastException or FormatException)
                {
                }
                finally
                {
                    row.Dispose();
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
        }

        return cpuLabeled ?? anyProbe;
    }

    private static IReadOnlyList<FanSensorSample> ReadFans()
    {
        var fans = new List<FanSensorSample>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                NamespacePath,
                "SELECT ElementName, DesiredSpeed FROM DCIM_Fan");
            using var rows = searcher.Get();
            foreach (ManagementObject row in rows)
            {
                try
                {
                    var rpm = Convert.ToInt32(row["DesiredSpeed"]);
                    if (rpm is <= 0 or >= 20000)
                    {
                        continue;
                    }

                    var elementName = Convert.ToString(row["ElementName"]) ?? string.Empty;
                    fans.Add(new FanSensorSample(NormalizeFanName(elementName, fans.Count), rpm));
                }
                catch (Exception ex) when (ex is ManagementException or InvalidCastException or FormatException)
                {
                }
                finally
                {
                    row.Dispose();
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
        }

        return fans;
    }

    private static string NormalizeFanName(string elementName, int index)
    {
        if (elementName.Contains("cpu", StringComparison.OrdinalIgnoreCase))
        {
            return "CPU";
        }

        if (elementName.Contains("gpu", StringComparison.OrdinalIgnoreCase) ||
            elementName.Contains("video", StringComparison.OrdinalIgnoreCase))
        {
            return "GPU";
        }

        return elementName.Length > 0 ? elementName : $"Fan {index + 1}";
    }
}
