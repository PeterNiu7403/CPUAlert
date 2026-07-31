using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// Enumerates DXGI adapters and pairs each with the sum of its 3D-engine
/// performance-counter instances (matched by adapter LUID). This is how the
/// dashboard can split 独显 (discrete) from 集显 (integrated) instead of
/// showing one blended "GPU Engine" number.
/// </summary>
public sealed partial class WindowsGpuAdapterService : IGpuAdapterService
{
    private const uint VendorIntel = 0x8086;
    private const uint VendorNvidia = 0x10DE;
    private const uint VendorAmdA = 0x1002;
    private const uint VendorAmdB = 0x1022;

    private IReadOnlyList<GpuAdapterInfo>? _adapters;

    public IReadOnlyList<GpuAdapterTelemetry> CaptureAdapters()
    {
        var adapters = GetAdapters();
        if (adapters.Count == 0)
        {
            return [];
        }

        var utilizationByLuid = Read3DUtilizationByLuid();
        var result = new List<GpuAdapterTelemetry>(adapters.Count);
        foreach (var adapter in adapters)
        {
            utilizationByLuid.TryGetValue(adapter.LuidKey, out var usage);
            result.Add(new GpuAdapterTelemetry(
                adapter.Name,
                adapter.Kind,
                Math.Clamp(usage, 0, 100),
                TemperatureCelsius: null)
            {
                VendorId = adapter.VendorId
            });
        }

        return result
            .OrderByDescending(a => a.Kind == GpuAdapterKind.Discrete)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<GpuAdapterInfo> GetAdapters()
    {
        if (_adapters is not null)
        {
            return _adapters;
        }

        _adapters = EnumerateDxgiAdapters();
        return _adapters;
    }

    private static IReadOnlyList<GpuAdapterInfo> EnumerateDxgiAdapters()
    {
        var adapters = new List<GpuAdapterInfo>();
        IDXGIFactory1? factory = null;
        try
        {
            var iid = typeof(IDXGIFactory1).GUID;
            var hr = CreateDXGIFactory1(in iid, out factory);
            if (hr != 0 || factory is null)
            {
                return adapters;
            }

            for (uint index = 0; ; index++)
            {
                IDXGIAdapter1? adapter = null;
                try
                {
                    hr = factory.EnumAdapters1(index, out adapter);
                    if (hr != 0 || adapter is null)
                    {
                        break;
                    }

                    hr = adapter.GetDesc1(out var desc);
                    if (hr != 0)
                    {
                        continue;
                    }

                    // Skip the Basic Render / software adapter.
                    if ((desc.Flags & DxgiAdapterFlagSoftware) != 0)
                    {
                        continue;
                    }

                    var name = string.IsNullOrWhiteSpace(desc.Description)
                        ? "GPU"
                        : desc.Description.Trim();
                    adapters.Add(new GpuAdapterInfo(
                        name,
                        Classify(desc.VendorId, name),
                        LuidKey(desc.AdapterLuidHighPart, desc.AdapterLuidLowPart),
                        desc.VendorId));
                }
                finally
                {
                    if (adapter is not null)
                    {
                        Marshal.ReleaseComObject(adapter);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is COMException or DllNotFoundException or EntryPointNotFoundException)
        {
        }
        finally
        {
            if (factory is not null)
            {
                Marshal.ReleaseComObject(factory);
            }
        }

        return adapters;
    }

    private static GpuAdapterKind Classify(uint vendorId, string name)
    {
        if (vendorId == VendorIntel)
        {
            return GpuAdapterKind.Integrated;
        }

        if (vendorId == VendorNvidia)
        {
            return GpuAdapterKind.Discrete;
        }

        if (vendorId is VendorAmdA or VendorAmdB)
        {
            // AMD brands its iGPUs "Radeon(TM) Graphics"; dGPUs carry RX/PRO names.
            return name.Contains("Graphics", StringComparison.OrdinalIgnoreCase)
                ? GpuAdapterKind.Integrated
                : GpuAdapterKind.Discrete;
        }

        return GpuAdapterKind.Unknown;
    }

    private static Dictionary<long, double> Read3DUtilizationByLuid()
    {
        var totals = new Dictionary<long, double>();
        try
        {
            const string categoryName = "GPU Engine";
            const string counterName = "Utilization Percentage";
            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                return totals;
            }

            var category = new PerformanceCounterCategory(categoryName);
            foreach (var instanceName in category.GetInstanceNames())
            {
                if (!instanceName.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var match = LuidPattern().Match(instanceName);
                if (!match.Success)
                {
                    continue;
                }

                if (!int.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out var high) ||
                    !uint.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.HexNumber, null, out var low))
                {
                    continue;
                }

                using var counter = new PerformanceCounter(categoryName, counterName, instanceName, readOnly: true);
                float value;
                try
                {
                    value = counter.NextValue();
                }
                catch
                {
                    continue;
                }

                var key = LuidKey(high, low);
                totals[key] = totals.TryGetValue(key, out var existing) ? existing + value : value;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }

        return totals;
    }

    private static long LuidKey(int highPart, uint lowPart) => ((long)highPart << 32) | lowPart;

    private const uint DxgiAdapterFlagSoftware = 2;

    [GeneratedRegex(@"luid_0x([0-9a-fA-F]+)_0x([0-9a-fA-F]+)", RegexOptions.IgnoreCase)]
    private static partial Regex LuidPattern();

    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory1(in Guid riid, out IDXGIFactory1 ppFactory);

    private sealed record GpuAdapterInfo(string Name, GpuAdapterKind Kind, long LuidKey, uint VendorId);

    [ComImport]
    [Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIFactory1
    {
        // IDXGIObject base
        [PreserveSig]
        int SetPrivateData(in Guid name, uint dataSize, IntPtr pData);

        [PreserveSig]
        int SetPrivateDataInterface(in Guid name, IntPtr pUnknown);

        [PreserveSig]
        int GetPrivateData(in Guid name, ref uint pDataSize, IntPtr pData);

        [PreserveSig]
        int GetParent(in Guid riid, out IntPtr ppParent);

        // IDXGIFactory base
        [PreserveSig]
        int EnumAdapters(uint adapter, out IntPtr ppAdapter);

        [PreserveSig]
        int MakeWindowAssociation(IntPtr windowHandle, uint flags);

        [PreserveSig]
        int GetWindowAssociation(out IntPtr pWindowHandle);

        [PreserveSig]
        int CreateSwapChain(IntPtr pDevice, IntPtr pDesc, IntPtr pFullscreenDesc, IntPtr pRestrictToOutput, out IntPtr ppSwapChain);

        [PreserveSig]
        int CreateSoftwareAdapter(IntPtr module, out IntPtr ppAdapter);

        // IDXGIFactory1
        [PreserveSig]
        int EnumAdapters1(uint adapter, out IDXGIAdapter1 ppAdapter);
    }

    [ComImport]
    [Guid("29038f61-3839-4626-91fd-086879011a05")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter1
    {
        // IDXGIObject base
        [PreserveSig]
        int SetPrivateData(in Guid name, uint dataSize, IntPtr pData);

        [PreserveSig]
        int SetPrivateDataInterface(in Guid name, IntPtr pUnknown);

        [PreserveSig]
        int GetPrivateData(in Guid name, ref uint pDataSize, IntPtr pData);

        [PreserveSig]
        int GetParent(in Guid riid, out IntPtr ppParent);

        // IDXGIAdapter base
        [PreserveSig]
        int EnumOutputs(uint output, out IntPtr ppOutput);

        [PreserveSig]
        int GetDesc(out DXGIAdapterDesc pDesc);

        [PreserveSig]
        int CheckInterfaceSupport(in Guid interfaceName, out long pUMDVersion);

        // IDXGIAdapter1
        [PreserveSig]
        int GetDesc1(out DXGIAdapterDesc1 pDesc);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGIAdapterDesc
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public uint AdapterLuidLowPart;
        public int AdapterLuidHighPart;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGIAdapterDesc1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public uint AdapterLuidLowPart;
        public int AdapterLuidHighPart;
        public uint Flags;
    }
}
