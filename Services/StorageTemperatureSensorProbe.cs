using System.Management;
using System.Runtime.InteropServices;
using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// Reads physical-drive temperatures without admin rights. Primary channel:
/// IOCTL_STORAGE_QUERY_PROPERTY with StorageDeviceTemperatureProperty on
/// \\.\PhysicalDriveN (opens with access=0 as a standard user; support depends
/// on the storage driver). Fallback: the WMI storage provider's
/// MSFT_StorageReliabilityCounter. Drive letters are mapped via MSFT_Partition
/// so temperatures can be attached to volumes. Refreshes at most once a
/// minute — drive temperatures drift slowly and the queries are not free.
/// </summary>
public sealed class StorageTemperatureSensorProbe : ISensorProbe
{
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const int StorageDeviceTemperatureProperty = 22;
    private const int MaxPhysicalDrives = 16;
    private const short TemperatureNotReported = unchecked((short)0x8000);

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);

    private readonly object _sync = new();
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private IReadOnlyList<DriveTemperatureSample> _cached = [];

    public string Name => "Storage";

    public SensorProbeResult Capture()
    {
        lock (_sync)
        {
            if (DateTime.UtcNow - _lastRefreshUtc < RefreshInterval)
            {
                return ToResult(_cached);
            }

            _lastRefreshUtc = DateTime.UtcNow;
            try
            {
                _cached = Refresh();
            }
            catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException or InvalidOperationException)
            {
                _cached = [];
            }

            return ToResult(_cached);
        }
    }

    private static SensorProbeResult ToResult(IReadOnlyList<DriveTemperatureSample> drives) =>
        drives.Count == 0
            ? SensorProbeResult.Empty
            : new SensorProbeResult(null, null, [], [], null, drives);

    private static IReadOnlyList<DriveTemperatureSample> Refresh()
    {
        var temperaturesByDisk = ReadIoctlTemperatures();
        foreach (var (disk, celsius) in ReadWmiTemperatures())
        {
            // The IOCTL descriptor is the more current source; WMI fills gaps.
            temperaturesByDisk.TryAdd(disk, celsius);
        }

        if (temperaturesByDisk.Count == 0)
        {
            return [];
        }

        var lettersByDisk = ReadDriveLettersByDisk();
        var samples = new List<DriveTemperatureSample>();
        foreach (var (disk, celsius) in temperaturesByDisk.OrderBy(pair => pair.Key))
        {
            if (lettersByDisk.TryGetValue(disk, out var letters) && letters.Count > 0)
            {
                foreach (var letter in letters)
                {
                    samples.Add(new DriveTemperatureSample(letter, celsius));
                }
            }
            else
            {
                samples.Add(new DriveTemperatureSample($"磁盘 {disk}", celsius));
            }
        }

        return samples;
    }

    private static Dictionary<int, double> ReadIoctlTemperatures()
    {
        var result = new Dictionary<int, double>();
        for (var disk = 0; disk < MaxPhysicalDrives; disk++)
        {
            var celsius = QueryDriveTemperature(disk);
            if (celsius.HasValue)
            {
                result[disk] = celsius.Value;
            }
        }

        return result;
    }

    private static double? QueryDriveTemperature(int disk)
    {
        var handle = CreateFile(
            $@"\\.\PhysicalDrive{disk}",
            0,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);
        if (handle == InvalidHandleValue)
        {
            return null;
        }

        try
        {
            var input = new byte[12]; // STORAGE_PROPERTY_QUERY: PropertyId(4) QueryType(4) AdditionalParameters(1 padded)
            BitConverter.GetBytes(StorageDeviceTemperatureProperty).CopyTo(input, 0);
            var output = new byte[4096];
            if (!DeviceIoControl(handle, IoctlStorageQueryProperty, input, (uint)input.Length, output, (uint)output.Length, out _, IntPtr.Zero))
            {
                return null;
            }

            // STORAGE_TEMPERATURE_DATA_DESCRIPTOR: Version(4) Size(4) Critical(2)
            // Warning(2) InfoCount(2) Reserved(10), then 16-byte info records.
            var count = BitConverter.ToInt16(output, 12);
            double? best = null;
            for (var i = 0; i < count && i < 8; i++)
            {
                var offset = 24 + i * 16;
                var raw = BitConverter.ToInt16(output, offset + 2);
                var celsius = DecodeTenthDegree(raw);
                if (celsius.HasValue)
                {
                    best = best is null ? celsius : Math.Max(best.Value, celsius.Value);
                }
            }

            return best;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>
    /// Decodes a STORAGE_TEMPERATURE_INFO value. The descriptor documents
    /// tenths of a degree but stacks differ on the unit (some report 0.1 K,
    /// others 0.1 °C); disambiguate by range — 0.1 K values for plausible
    /// drive temperatures are always ≥ ~2400, 0.1 °C values always ≤ 1300.
    /// </summary>
    public static double? DecodeTenthDegree(short raw)
    {
        if (raw == TemperatureNotReported)
        {
            return null;
        }

        var celsius = raw >= 1500
            ? raw / 10.0 - 273.15
            : raw / 10.0;

        return celsius is > -40 and < 130 ? celsius : null;
    }

    private static Dictionary<int, double> ReadWmiTemperatures()
    {
        var result = new Dictionary<int, double>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\Microsoft\Windows\Storage",
                "SELECT DeviceId, Temperature FROM MSFT_StorageReliabilityCounter");
            using var rows = searcher.Get();
            foreach (ManagementObject row in rows)
            {
                try
                {
                    var deviceId = Convert.ToString(row["DeviceId"]) ?? string.Empty;
                    var temperature = Convert.ToInt32(row["Temperature"]);
                    if (int.TryParse(deviceId, out var disk) && temperature is > 0 and < 130)
                    {
                        result[disk] = temperature;
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
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException)
        {
        }

        return result;
    }

    private static Dictionary<int, List<string>> ReadDriveLettersByDisk()
    {
        var result = new Dictionary<int, List<string>>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\Microsoft\Windows\Storage",
                "SELECT DiskNumber, DriveLetter FROM MSFT_Partition");
            using var rows = searcher.Get();
            foreach (ManagementObject row in rows)
            {
                try
                {
                    var disk = Convert.ToInt32(row["DiskNumber"]);
                    var letterValue = Convert.ToUInt16(row["DriveLetter"]);
                    if (letterValue is >= (ushort)'A' and <= (ushort)'Z')
                    {
                        var letter = $"{(char)letterValue}:";
                        if (!result.TryGetValue(disk, out var letters))
                        {
                            letters = [];
                            result[disk] = letters;
                        }

                        letters.Add(letter);
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
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException)
        {
        }

        return result;
    }

    private static readonly IntPtr InvalidHandleValue = new(-1);
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        byte[] lpInBuffer,
        uint nInBufferSize,
        byte[] lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
