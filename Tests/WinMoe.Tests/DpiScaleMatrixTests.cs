using WinMoe.Ui;
using Xunit;

namespace WinMoe.Tests;

public sealed class DpiScaleMatrixTests
{
    [Fact]
    public void All_CoversFourWindowsScales()
    {
        Assert.Equal([100, 125, 150, 200], DpiScaleMatrix.All.Select(point => point.Percent));
        Assert.Equal([96u, 120u, 144u, 192u], DpiScaleMatrix.All.Select(point => point.Dpi));
    }

    [Theory]
    [InlineData(100, 1194, 768, 340, 720)]
    [InlineData(125, 1493, 960, 425, 900)]
    [InlineData(150, 1791, 1152, 510, 1080)]
    [InlineData(200, 2388, 1536, 680, 1440)]
    public void ScalePoint_MatchesWindowSizing(
        int percent,
        int mainW,
        int mainH,
        int hudW,
        int hudH)
    {
        var point = DpiScaleMatrix.FindByPercent(percent);
        Assert.NotNull(point);
        Assert.Equal(new WindowPixelSize(mainW, mainH), point!.MainWindowPhysical);
        Assert.Equal(new WindowPixelSize(hudW, hudH), point.TrayHudPhysical);
    }

    [Fact]
    public void Nearest_PicksClosestDpi()
    {
        Assert.Equal(144u, DpiScaleMatrix.Nearest(140).Dpi);
        Assert.Equal(96u, DpiScaleMatrix.Nearest(0).Dpi);
    }

    [Fact]
    public void FormatReport_ContainsTableRows()
    {
        var report = DpiScaleMatrix.FormatReport();
        Assert.Contains("1791×1152", report);
        Assert.Contains("510×1080", report);
    }
}
