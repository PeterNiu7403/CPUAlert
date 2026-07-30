using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class WinMoeStartupOptionsTests
{
    [Fact]
    public void Parse_DefaultsToNormalStartup()
    {
        var options = WinMoeStartupOptions.Parse(null, _ => null);

        Assert.False(options.ShowTrayHudDiagnostic);
        Assert.False(options.DisableTray);
        Assert.Null(options.InitialRoute);
    }

    [Fact]
    public void Parse_ReadsDiagnosticFlagsFromArguments()
    {
        var options = WinMoeStartupOptions.Parse("--show-tray-hud --no-tray", _ => null);

        Assert.True(options.ShowTrayHudDiagnostic);
        Assert.True(options.DisableTray);
    }

    [Fact]
    public void Parse_ReadsDiagnosticFlagsFromEnvironment()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["WINMOE_SHOW_TRAY_HUD"] = "true",
            ["WINMOE_DISABLE_TRAY"] = "1",
            ["WINMOE_START_ROUTE"] = "purge"
        };

        var options = WinMoeStartupOptions.Parse(string.Empty, name => values.GetValueOrDefault(name));

        Assert.True(options.ShowTrayHudDiagnostic);
        Assert.True(options.DisableTray);
        Assert.Equal("purge", options.InitialRoute);
    }

    [Fact]
    public void Parse_AcceptsLegacyEnvironmentNames()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["MOLEWINDOWS_SHOW_TRAY_HUD"] = "true",
            ["MOLEWINDOWS_DISABLE_TRAY"] = "1",
            ["MOLEWINDOWS_START_ROUTE"] = "status"
        };

        var options = WinMoeStartupOptions.Parse(string.Empty, name => values.GetValueOrDefault(name));

        Assert.True(options.ShowTrayHudDiagnostic);
        Assert.True(options.DisableTray);
        Assert.Equal("status", options.InitialRoute);
    }

    [Fact]
    public void Parse_ReadsInitialRouteFromArguments()
    {
        var options = WinMoeStartupOptions.Parse("--route=history", _ => null);

        Assert.Equal("history", options.InitialRoute);
    }
}
