namespace WinMoe.Models;

public enum GpuAdapterKind
{
    Unknown,
    Integrated,
    Discrete
}

/// <summary>Per-adapter GPU state (Mole splits a single GPU; Windows splits dGPU/iGPU).</summary>
public sealed record GpuAdapterTelemetry(
    string Name,
    GpuAdapterKind Kind,
    double Engine3DPercent,
    double? TemperatureCelsius)
{
    /// <summary>PCI vendor ID (0x10DE NVIDIA, 0x1002 AMD, 0x8086 Intel) for sensor matching.</summary>
    public uint VendorId { get; init; }
    /// <summary>Marketing name trimmed like Mole ("NVIDIA GeForce RTX 5080" → "RTX 5080").</summary>
    public string ShortName => Shorten(Name);

    public static string Shorten(string name)
    {
        var trimmed = name.Trim();
        trimmed = trimmed.Replace("NVIDIA GeForce", string.Empty, StringComparison.OrdinalIgnoreCase);
        trimmed = trimmed.Replace("(R)", string.Empty, StringComparison.Ordinal);
        trimmed = trimmed.Replace("(TM)", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (trimmed.EndsWith(" GPU", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        trimmed = trimmed.Replace("  ", " ", StringComparison.Ordinal).Trim();
        return trimmed.Length == 0 ? name.Trim() : trimmed;
    }
}
