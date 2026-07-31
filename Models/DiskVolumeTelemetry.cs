using WinMoe.Services;

namespace WinMoe.Models;

/// <summary>One fixed volume's capacity state — the partition-level view of disk space.</summary>
public sealed record DiskVolumeTelemetry(
    string RootPath,
    string VolumeLabel,
    long TotalBytes,
    long FreeBytes)
{
    /// <summary>Backing physical drive temperature when the storage stack reports it.</summary>
    public double? TemperatureCelsius { get; init; }
    /// <summary>Drive display text, e.g. "C:".</summary>
    public string LetterText => RootPath.TrimEnd('\\', '/');

    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);

    public double UsagePercent => TotalBytes > 0
        ? Math.Clamp(UsedBytes * 100d / TotalBytes, 0, 100)
        : 0;

    public string FreeText => SystemTelemetryFormatter.Bytes(FreeBytes);

    public string UsedOverTotalText =>
        $"{SystemTelemetryFormatter.Bytes(UsedBytes)} / {SystemTelemetryFormatter.Bytes(TotalBytes)}";

    /// <summary>"Windows (C:)" when a label exists, otherwise just "C:".</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(VolumeLabel)
        ? LetterText
        : $"{VolumeLabel} ({LetterText})";

    /// <summary>One-line partition summary: "150 GB / 223 GB · 可用 73 GB · 42°C".</summary>
    public string SummaryText => TemperatureCelsius is { } celsius
        ? $"{UsedOverTotalText} · 可用 {FreeText} · {celsius:0}°C"
        : $"{UsedOverTotalText} · 可用 {FreeText}";
}
