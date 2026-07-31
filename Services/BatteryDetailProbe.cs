using System.Management;
using System.Runtime.InteropServices;

namespace WinMoe.Services;

/// <summary>Static battery details from the firmware plus the live charge/discharge rate.</summary>
/// <param name="DesignCapacityMwh">Factory design capacity (mWh); null when the probe fails.</param>
/// <param name="FullChargeCapacityMwh">Current full-charge capacity (mWh); null when unavailable.</param>
/// <param name="CycleCount">Reported charge cycles; null when neither IOCTL nor WMI reports one.</param>
/// <param name="RateMw">Signed power in mW: charging positive, discharging negative; null when idle/unknown.</param>
public sealed record BatteryDetailSnapshot(
    long? DesignCapacityMwh,
    long? FullChargeCapacityMwh,
    int? CycleCount,
    int? RateMw);

/// <summary>
/// Reads battery design/full-charge capacity and cycle count through
/// IOCTL_BATTERY_QUERY_INFORMATION (the same unprivileged path powercfg's battery report
/// uses) and the live charge/discharge rate from root\WMI BatteryStatus. Every failure
/// maps to null — the UI hides the segment instead of fabricating a value. Static values
/// are cached once per process; the rate is queried on every call.
/// </summary>
public static class BatteryDetailProbe
{
    // GUID_DEVICE_BATTERY device interface class.
    private static Guid DeviceBatteryGuid = new("72631e54-78a4-11d0-bcf7-00aa00b7b32a");
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;
    private const uint IoctlBatteryQueryTag = 0x294040;          // FILE_DEVICE_BATTERY 0x29, function 0x10
    private const uint IoctlBatteryQueryInformation = 0x294044;  // FILE_DEVICE_BATTERY 0x29, function 0x11
    private const uint BatteryInformationLevel = 0; // BATTERY_QUERY_INFORMATION_LEVEL.BatteryInformation

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    // BATTERY_INFORMATION fixed layout (36 bytes); offsets ParseBatteryInformation reads.
    private const int BatteryInformationSize = 36;
    private const int DesignedCapacityOffset = 12;
    private const int FullChargedCapacityOffset = 16;
    private const int CycleCountOffset = 32;

    // Charge/discharge rates above this are firmware sentinel garbage (e.g. 0xFFFFFFFF).
    private const long MaxSaneRateMw = 10_000_000;

    private static readonly object StaticSync = new();
    private static bool _staticLoaded;
    private static long? _designCapacityMwh;
    private static long? _fullChargeCapacityMwh;
    private static int? _cycleCount;

    public static BatteryDetailSnapshot Query()
    {
        try
        {
            var (design, full, cycles) = GetStaticInfo();
            return new BatteryDetailSnapshot(design, full, cycles, QueryRateMw());
        }
        catch
        {
            // Any probe failure (driver, WMI, permissions) → honest "no data".
            return new BatteryDetailSnapshot(null, null, null, null);
        }
    }

    private static (long? Design, long? Full, int? Cycles) GetStaticInfo()
    {
        lock (StaticSync)
        {
            if (!_staticLoaded)
            {
                (_designCapacityMwh, _fullChargeCapacityMwh, _cycleCount) = ProbeStaticInfo();
                _staticLoaded = true;
            }

            return (_designCapacityMwh, _fullChargeCapacityMwh, _cycleCount);
        }
    }

    private static (long?, long?, int?) ProbeStaticInfo()
    {
        try
        {
            var info = ReadFirstBatteryInformation();
            // Some firmware reports CycleCount=0 through the IOCTL; WMI carries the real one.
            var cycles = info?.CycleCount ?? QueryCycleCountWmi();
            return (info?.DesignCapacityMwh, info?.FullChargeCapacityMwh, cycles);
        }
        catch
        {
            return (null, null, null);
        }
    }

    private static BatteryDetailSnapshot? ReadFirstBatteryInformation()
    {
        var deviceInfoSet = SetupDiGetClassDevsW(ref DeviceBatteryGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (deviceInfoSet == InvalidHandleValue)
        {
            return null;
        }

        try
        {
            // First battery only: SystemPowerStatus telemetry is single-battery as well.
            var interfaceData = new SpDeviceInterfaceData { cbSize = (uint)Marshal.SizeOf<SpDeviceInterfaceData>() };
            if (!SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref DeviceBatteryGuid, 0, ref interfaceData))
            {
                return null;
            }

            SetupDiGetDeviceInterfaceDetailW(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out var requiredSize, IntPtr.Zero);
            if (requiredSize == 0)
            {
                return null;
            }

            var detail = Marshal.AllocHGlobal((int)requiredSize);
            try
            {
                // SP_DEVICE_INTERFACE_DETAIL_DATA_W.cbSize is the fixed-part size: 8 (x64) / 6 (x86).
                Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                if (!SetupDiGetDeviceInterfaceDetailW(deviceInfoSet, ref interfaceData, detail, requiredSize, out _, IntPtr.Zero))
                {
                    return null;
                }

                // DevicePath begins right after the cbSize DWORD.
                var devicePath = Marshal.PtrToStringUni(detail + 4);
                return string.IsNullOrEmpty(devicePath) ? null : ReadBatteryInformation(devicePath);
            }
            finally
            {
                Marshal.FreeHGlobal(detail);
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static BatteryDetailSnapshot? ReadBatteryInformation(string devicePath)
    {
        // The battery driver rejects metadata IOCTLs on an access=0 handle
        // (ERROR_ACCESS_DENIED on this Lenovo); open read/write like powercfg does.
        var handle = CreateFileW(devicePath, GenericRead | GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle == InvalidHandleValue)
        {
            handle = CreateFileW(devicePath, 0, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        }

        if (handle == InvalidHandleValue)
        {
            return null;
        }

        try
        {
            return QueryBatteryTag(handle) is { } tag
                ? QueryBatteryInformation(handle, tag)
                : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static ulong? QueryBatteryTag(IntPtr handle)
    {
        // IOCTL_BATTERY_QUERY_TAG: 4-byte input (unused), 4-byte output tag.
        var input = Marshal.AllocHGlobal(sizeof(uint));
        var output = Marshal.AllocHGlobal(sizeof(uint));
        try
        {
            Marshal.WriteInt32(input, 0);
            Marshal.WriteInt32(output, 0);

            return DeviceIoControl(handle, IoctlBatteryQueryTag, input, sizeof(uint), output, sizeof(uint), out _, IntPtr.Zero)
                ? (ulong)(uint)Marshal.ReadInt32(output)
                : null;
        }
        finally
        {
            Marshal.FreeHGlobal(input);
            Marshal.FreeHGlobal(output);
        }
    }

    private static BatteryDetailSnapshot? QueryBatteryInformation(IntPtr handle, ulong tag)
    {
        var input = Marshal.AllocHGlobal(Marshal.SizeOf<BatteryQueryInformation>());
        var output = Marshal.AllocHGlobal(Marshal.SizeOf<BatteryInformation>());
        try
        {
            Marshal.StructureToPtr(new BatteryQueryInformation
            {
                BatteryTag = (uint)tag,
                InformationLevel = BatteryInformationLevel,
                AtRate = 0
            }, input, false);

            if (!DeviceIoControl(handle, IoctlBatteryQueryInformation, input, (uint)Marshal.SizeOf<BatteryQueryInformation>(), output, (uint)Marshal.SizeOf<BatteryInformation>(), out var returned, IntPtr.Zero) ||
                returned < BatteryInformationSize)
            {
                return null;
            }

            var buffer = new byte[BatteryInformationSize];
            Marshal.Copy(output, buffer, 0, buffer.Length);
            return ParseBatteryInformation(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(input);
            Marshal.FreeHGlobal(output);
        }
    }

    /// <summary>Maps the fixed 36-byte BATTERY_INFORMATION layout to a snapshot (RateMw stays null).</summary>
    internal static BatteryDetailSnapshot ParseBatteryInformation(byte[] buffer)
    {
        if (buffer is null || buffer.Length < BatteryInformationSize)
        {
            return new BatteryDetailSnapshot(null, null, null, null);
        }

        return new BatteryDetailSnapshot(
            ReadCapacity(buffer, DesignedCapacityOffset),
            ReadCapacity(buffer, FullChargedCapacityOffset),
            ReadCycleCount(buffer),
            null);
    }

    private static long? ReadCapacity(byte[] buffer, int offset)
    {
        var raw = BitConverter.ToUInt32(buffer, offset);
        // 0 and BATTERY_UNKNOWN_CAPACITY (0xFFFFFFFF) both mean "not reported".
        return raw is 0 or uint.MaxValue ? null : (long)raw;
    }

    private static int? ReadCycleCount(byte[] buffer)
    {
        var raw = BitConverter.ToUInt32(buffer, CycleCountOffset);
        return raw is 0 or uint.MaxValue ? null : (int)raw;
    }

    private static int? QueryRateMw()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\WMI",
                "SELECT Charging, Discharging, ChargeRate, DischargeRate FROM BatteryStatus");
            using var results = searcher.Get();
            using var status = results.Cast<ManagementObject>().FirstOrDefault();
            if (status is null)
            {
                return null;
            }

            var charging = status["Charging"] is bool isCharging && isCharging;
            var discharging = status["Discharging"] is bool isDischarging && isDischarging;
            var chargeRate = ToInt64(status["ChargeRate"]);
            var dischargeRate = ToInt64(status["DischargeRate"]);

            if (charging && chargeRate is > 0 and <= MaxSaneRateMw)
            {
                return (int)chargeRate.Value;
            }

            if (discharging && dischargeRate is > 0 and <= MaxSaneRateMw)
            {
                return -(int)dischargeRate.Value;
            }

            return null;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException or InvalidCastException)
        {
            return null;
        }
    }

    private static int? QueryCycleCountWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\WMI",
                "SELECT CycleCount FROM BatteryCycleCount");
            using var results = searcher.Get();
            using var row = results.Cast<ManagementObject>().FirstOrDefault();
            var cycles = row is null ? null : ToInt64(row["CycleCount"]);
            return cycles is > 0 and <= int.MaxValue ? (int)cycles.Value : null;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException or InvalidCastException)
        {
            return null;
        }
    }

    private static long? ToInt64(object? value)
    {
        try
        {
            return value is null ? null : Convert.ToInt64(value);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BatteryQueryInformation
    {
        public uint BatteryTag;
        public uint InformationLevel;
        public int AtRate;
    }

    // Win32 BATTERY_INFORMATION (36 bytes sequential): Capabilities DWORD, Technology byte,
    // Reserved[3], Chemistry[4], then six DWORDs — DesignedCapacity, FullChargedCapacity,
    // DefaultAlert1, DefaultAlert2, CriticalBias, CycleCount. Parse reads by offset.
    [StructLayout(LayoutKind.Sequential)]
    private struct BatteryInformation
    {
        public uint Capabilities;
        public byte Technology;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Reserved;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Chemistry;
        public uint DesignedCapacity;
        public uint FullChargedCapacity;
        public uint DefaultAlert1;
        public uint DefaultAlert2;
        public uint CriticalBias;
        public uint CycleCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        public uint cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SpDeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr deviceInfoSet, ref SpDeviceInterfaceData deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
