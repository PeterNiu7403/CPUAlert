namespace WinMoe.Services;

/// <summary>
/// Honest capability matrix for Mole features that map poorly to Windows APIs.
/// Used by Status/HUD empty-states and environment verification docs.
/// </summary>
public static class WindowsPlatformCapabilities
{
    public sealed record Capability(
        string Id,
        string MoleFeature,
        string WindowsStatus,
        bool UiSurfaceReady,
        bool DataAvailable,
        string Notes);

    public static IReadOnlyList<Capability> All { get; } =
    [
        new(
            "fan-rpm",
            "风扇转速 / Auto·Cool·Max",
            "unavailable",
            UiSurfaceReady: true,
            DataAvailable: false,
            Notes: "Windows 无 SMC 等价接口；UI 展示自动分段，数值为 —。"),
        new(
            "battery-accessories",
            "配件电池（耳机等）",
            "unavailable",
            UiSurfaceReady: false,
            DataAvailable: false,
            Notes: "无通用系统 API；不伪造配件卡。"),
        new(
            "battery-main",
            "主机电池电量/状态",
            "available",
            UiSurfaceReady: true,
            DataAvailable: true,
            Notes: "GetSystemPowerStatus；台式可能无电池。"),
        new(
            "silent-app-updates",
            "Apps 静默更新列表",
            "unavailable",
            UiSurfaceReady: true,
            DataAvailable: false,
            Notes: "无可信非交互更新源；安静空态，不静默安装。"),
        new(
            "startup-inventory",
            "启动项清单",
            "read-only",
            UiSurfaceReady: true,
            DataAvailable: true,
            Notes: "Run 键 + 启动文件夹只读；不写注册表。"),
        new(
            "gpu-engine",
            "GPU 引擎占用",
            "best-effort",
            UiSurfaceReady: true,
            DataAvailable: true,
            Notes: "性能计数器可用时显示，否则 Unavailable。"),
        new(
            "process-energy",
            "进程能耗 (PWR)",
            "proxy",
            UiSurfaceReady: true,
            DataAvailable: true,
            Notes: "无 Energy Impact；用 CPU 代理，空闲为 —。"),
        new(
            "multi-monitor-dpi",
            "混合 DPI 多显示器 HUD 锚定",
            "supported",
            UiSurfaceReady: true,
            DataAvailable: true,
            Notes: "HUD 按锚点显示器 DPI 调整物理尺寸与边距。")
    ];

    public static Capability? Find(string id)
        => All.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));

    public static string FormatMarkdownTable()
    {
        var lines = new List<string>
        {
            "| Id | Mole | Windows | UI | Data | Notes |",
            "| --- | --- | --- | --- | --- | --- |"
        };

        foreach (var item in All)
        {
            lines.Add(
                $"| `{item.Id}` | {item.MoleFeature} | {item.WindowsStatus} | {(item.UiSurfaceReady ? "✓" : "—")} | {(item.DataAvailable ? "✓" : "—")} | {item.Notes} |");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
