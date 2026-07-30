using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinMoe.Models;
using WinMoe.Services;

namespace WinMoe.ViewModels;

public sealed class DiskTreemapTileViewModel
{
    // DaisyDisk-style earth tones sampled from mole.fit analyze.jpg
    private static readonly string[] Palette =
    [
        "#C6A771",
        "#B88448",
        "#86727E",
        "#A1553B",
        "#83717F",
        "#615348",
        "#D0B080",
        "#9A6B4A"
    ];

    public DiskTreemapTileViewModel(DiskTreemapRect rect)
    {
        Name = rect.Name;
        Path = rect.Path;
        SizeText = SystemTelemetryFormatter.Bytes(rect.SizeBytes);
        X = rect.X;
        Y = rect.Y;
        Width = rect.Width;
        Height = rect.Height;
        FontSize = rect.Width > 220 && rect.Height > 150 ? 15 : 12;
        ShowDetail = rect.Width > 100 && rect.Height > 64;
        LabelOpacity = ShowDetail ? 1 : 0;
        IconVisibility = rect.Width > 140 && rect.Height > 90 ? Visibility.Visible : Visibility.Collapsed;
        FillBrush = new SolidColorBrush(ParseColor(Palette[rect.ColorIndex % Palette.Length]));
    }

    public string Name { get; }

    public string Path { get; }

    public string SizeText { get; }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public double FontSize { get; }

    public bool ShowDetail { get; }

    public double LabelOpacity { get; }

    public Visibility IconVisibility { get; }

    public SolidColorBrush FillBrush { get; }

    private static Windows.UI.Color ParseColor(string hex)
    {
        var value = Convert.ToUInt32(hex.TrimStart('#'), 16);
        return Windows.UI.Color.FromArgb(
            255,
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF));
    }
}
