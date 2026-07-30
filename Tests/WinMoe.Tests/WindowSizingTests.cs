using WinMoe.Ui;
using Xunit;

namespace WinMoe.Tests;

public sealed class WindowSizingTests
{
    [Theory]
    [InlineData(96u, 1194, 768)]
    [InlineData(120u, 1493, 960)]
    [InlineData(144u, 1791, 1152)]
    [InlineData(192u, 2388, 1536)]
    public void ToPhysicalPixels_ScalesDipWindowForCurrentDpi(
        uint dpi,
        int expectedWidth,
        int expectedHeight)
    {
        var size = WindowSizing.ToPhysicalPixels(1194, 768, dpi);

        Assert.Equal(expectedWidth, size.Width);
        Assert.Equal(expectedHeight, size.Height);
    }
}
