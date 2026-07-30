namespace WinMoe.Ui;

public readonly record struct WindowPixelPosition(int Left, int Top);

public readonly record struct TrayHudLayoutMetrics(
    WindowPixelSize ClientSize,
    int ScreenEdgeInset,
    int AnchorOffset)
{
    public WindowPixelPosition PositionNear(
        int anchorX,
        int anchorY,
        int outerWidth,
        int outerHeight)
    {
        return new WindowPixelPosition(
            Math.Max(ScreenEdgeInset, anchorX - outerWidth - AnchorOffset),
            Math.Max(ScreenEdgeInset, anchorY - outerHeight - AnchorOffset));
    }
}

public static class TrayHudLayout
{
    public const int WidthInDips = DpiScaleMatrix.TrayHudWidthDips;
    public const int HeightInDips = DpiScaleMatrix.TrayHudHeightDips;
    private const int ScreenEdgeInsetInDips = 8;
    private const int AnchorOffsetInDips = 12;

    public static TrayHudLayoutMetrics ForDpi(uint dpi)
    {
        var clientSize = WindowSizing.ToPhysicalPixels(WidthInDips, HeightInDips, dpi);
        var spacing = WindowSizing.ToPhysicalPixels(
            ScreenEdgeInsetInDips,
            AnchorOffsetInDips,
            dpi);

        return new TrayHudLayoutMetrics(
            clientSize,
            ScreenEdgeInset: spacing.Width,
            AnchorOffset: spacing.Height);
    }

    /// <summary>
    /// Prefer the monitor under the tray/cursor anchor (mixed-DPI aware), not the
    /// monitor that currently hosts a previously shown HUD window.
    /// </summary>
    public static TrayHudLayoutMetrics ForAnchorPoint(int anchorX, int anchorY, uint fallbackDpi = 96)
    {
        var dpi = DisplayDpi.GetDpiForPoint(anchorX, anchorY, fallbackDpi);
        return ForDpi(dpi);
    }
}
