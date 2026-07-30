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
    private const int WidthInDips = 430;
    private const int HeightInDips = 860;
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
}
