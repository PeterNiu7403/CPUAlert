namespace WinMoe.Ui;

public readonly record struct WindowPixelSize(int Width, int Height);

public static class WindowSizing
{
    private const double DefaultDpi = 96d;

    public static WindowPixelSize ToPhysicalPixels(int widthInDips, int heightInDips, uint dpi)
    {
        return new WindowPixelSize(
            ScaleToPhysicalPixels(widthInDips, dpi),
            ScaleToPhysicalPixels(heightInDips, dpi));
    }

    private static int ScaleToPhysicalPixels(int valueInDips, uint dpi)
    {
        return checked((int)Math.Round(
            valueInDips * dpi / DefaultDpi,
            MidpointRounding.AwayFromZero));
    }
}
