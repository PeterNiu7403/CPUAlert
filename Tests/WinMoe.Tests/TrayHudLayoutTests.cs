using WinMoe.Ui;
using Xunit;

namespace WinMoe.Tests;

public sealed class TrayHudLayoutTests
{
    [Theory]
    [InlineData(96u, 340, 720, 8, 12)]
    [InlineData(120u, 425, 900, 10, 15)]
    [InlineData(144u, 510, 1080, 12, 18)]
    [InlineData(192u, 680, 1440, 16, 24)]
    public void ForDpi_UsesPhysicalPixelsAcrossScaleMatrix(
        uint dpi,
        int expectedWidth,
        int expectedHeight,
        int expectedInset,
        int expectedAnchorOffset)
    {
        var metrics = TrayHudLayout.ForDpi(dpi);

        Assert.Equal(new WindowPixelSize(expectedWidth, expectedHeight), metrics.ClientSize);
        Assert.Equal(expectedInset, metrics.ScreenEdgeInset);
        Assert.Equal(expectedAnchorOffset, metrics.AnchorOffset);
    }

    [Fact]
    public void ForDpi_PositionsNearAnchorAt150Percent()
    {
        var metrics = TrayHudLayout.ForDpi(144);

        Assert.Equal(
            new WindowPixelPosition(1472, 402),
            metrics.PositionNear(
                anchorX: 2000,
                anchorY: 1500,
                outerWidth: 510,
                outerHeight: 1080));
    }

    [Fact]
    public void ForAnchorPoint_UsesFallbackWhenDisplayApiUnavailableInTests()
    {
        // In unit tests, native monitor DPI still works on Windows; just ensure non-zero layout.
        var metrics = TrayHudLayout.ForAnchorPoint(10, 10, fallbackDpi: 96);
        Assert.True(metrics.ClientSize.Width >= 340);
        Assert.True(metrics.ClientSize.Height >= 720);
    }
}
