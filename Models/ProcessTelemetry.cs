using System.Globalization;
using WinMoe.Services;

namespace WinMoe.Models;

public sealed record ProcessTelemetry(
    string Name,
    int ProcessId,
    long WorkingSetBytes,
    double CpuUsagePercent = 0,
    double TotalProcessorSeconds = 0,
    bool IsPinned = false)
{
    public string WorkingSetText => SystemTelemetryFormatter.Bytes(WorkingSetBytes);

    public string CpuUsageText => SystemTelemetryFormatter.Percent(CpuUsagePercent);

    /// <summary>Narrow gold bar under Mole process table (target ~40 dip full scale).</summary>
    public double CpuBarWidth => Math.Clamp(CpuUsagePercent, 0, 100) / 100 * 40;

    /// <summary>
    /// Windows has no Apple-style energy impact; show a light proxy when CPU is meaningful,
    /// otherwise an em dash like Mole's idle rows.
    /// </summary>
    public string PowerImpactText =>
        CpuUsagePercent < 2d
            ? "—"
            : (CpuUsagePercent * 0.75d).ToString("0.#", CultureInfo.InvariantCulture);

    public string Initials =>
        string.IsNullOrWhiteSpace(Name) ? "?" : char.ToUpperInvariant(Name.Trim()[0]).ToString();

    /// <summary>Segoe MDL2 pin when fixed to top.</summary>
    public string PinGlyph => IsPinned ? "\uE718" : string.Empty;

    public string PinOpacity => IsPinned ? "1" : "0";
}
