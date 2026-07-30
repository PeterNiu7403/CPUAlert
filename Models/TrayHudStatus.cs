namespace WinMoe.Models;

public sealed record TrayHudStatus(
    string SampleText,
    string HealthScore,
    string HealthLabel,
    string CpuText,
    string MemoryText,
    string DiskText,
    string NetworkText,
    string ActivityTitle,
    string ActivityDetail,
    IReadOnlyList<ProcessTelemetry> TopProcesses,
    string LifetimeCleanedText = "—",
    string LifetimeUninstalledText = "—",
    string LifetimeOptimizedText = "—",
    string DeviceChipText = "Windows",
    string GpuText = "—",
    string FanText = "—",
    string MemoryDetailText = "",
    string DiskDetailText = "",
    string NetworkDetailText = "");
