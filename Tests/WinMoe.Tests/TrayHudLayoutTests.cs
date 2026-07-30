using WinMoe.Ui;
using Xunit;

namespace WinMoe.Tests;

public sealed class TrayHudLayoutTests
{
    [Fact]
    public void ForDpi_UsesPhysicalPixelsForHudSizeAndPlacementAt150Percent()
    {
        var metrics = TrayHudLayout.ForDpi(144);

        Assert.Equal(new WindowPixelSize(645, 1290), metrics.ClientSize);
        Assert.Equal(12, metrics.ScreenEdgeInset);
        Assert.Equal(18, metrics.AnchorOffset);
        Assert.Equal(
            new WindowPixelPosition(1332, 182),
            metrics.PositionNear(
                anchorX: 2000,
                anchorY: 1500,
                outerWidth: 650,
                outerHeight: 1300));
    }
}
