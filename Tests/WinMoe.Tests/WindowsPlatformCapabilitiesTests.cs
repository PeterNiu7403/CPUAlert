using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class WindowsPlatformCapabilitiesTests
{
    [Fact]
    public void All_IncludesCoreBoundaries()
    {
        Assert.Contains(WindowsPlatformCapabilities.All, item => item.Id == "fan-rpm" && !item.DataAvailable);
        Assert.Contains(WindowsPlatformCapabilities.All, item => item.Id == "battery-main" && item.UiSurfaceReady);
        Assert.Contains(WindowsPlatformCapabilities.All, item => item.Id == "silent-app-updates" && !item.DataAvailable);
        Assert.Contains(WindowsPlatformCapabilities.All, item => item.Id == "multi-monitor-dpi" && item.DataAvailable);
    }

    [Fact]
    public void FormatMarkdownTable_IsNonEmpty()
    {
        var table = WindowsPlatformCapabilities.FormatMarkdownTable();
        Assert.Contains("`fan-rpm`", table);
        Assert.Contains("Windows", table);
    }
}
