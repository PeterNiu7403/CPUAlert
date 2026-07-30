namespace WinMoe.Ui;

/// <summary>
/// Canonical DIP → physical pixel expectations for Mole-parity window surfaces
/// across Windows display scales (100/125/150/200%).
/// </summary>
public static class DpiScaleMatrix
{
    public const int MainWindowWidthDips = 1194;
    public const int MainWindowHeightDips = 768;
    public const int TrayHudWidthDips = 340;
    public const int TrayHudHeightDips = 720;

    public sealed record ScalePoint(
        string Label,
        int Percent,
        uint Dpi,
        WindowPixelSize MainWindowPhysical,
        WindowPixelSize TrayHudPhysical)
    {
        public double ScaleFactor => Dpi / 96d;
    }

    public static IReadOnlyList<ScalePoint> All { get; } =
    [
        Create("100%", 100, 96),
        Create("125%", 125, 120),
        Create("150%", 150, 144),
        Create("200%", 200, 192)
    ];

    public static ScalePoint? FindByPercent(int percent)
        => All.FirstOrDefault(point => point.Percent == percent);

    public static ScalePoint? FindByDpi(uint dpi)
        => All.FirstOrDefault(point => point.Dpi == dpi);

    public static ScalePoint Nearest(uint dpi)
    {
        if (dpi == 0)
        {
            return All[0];
        }

        return All
            .OrderBy(point => Math.Abs((int)point.Dpi - (int)dpi))
            .First();
    }

    public static string FormatReport()
    {
        var lines = new List<string>
        {
            "# DPI layout matrix (DIP → physical)",
            "",
            $"| Scale | DPI | Main {MainWindowWidthDips}×{MainWindowHeightDips} DIP | HUD {TrayHudWidthDips}×{TrayHudHeightDips} DIP |",
            "| --- | ---: | ---: | ---: |"
        };

        foreach (var point in All)
        {
            lines.Add(
                $"| {point.Label} | {point.Dpi} | {point.MainWindowPhysical.Width}×{point.MainWindowPhysical.Height} | {point.TrayHudPhysical.Width}×{point.TrayHudPhysical.Height} |");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static ScalePoint Create(string label, int percent, uint dpi)
    {
        return new ScalePoint(
            label,
            percent,
            dpi,
            WindowSizing.ToPhysicalPixels(MainWindowWidthDips, MainWindowHeightDips, dpi),
            WindowSizing.ToPhysicalPixels(TrayHudWidthDips, TrayHudHeightDips, dpi));
    }
}
