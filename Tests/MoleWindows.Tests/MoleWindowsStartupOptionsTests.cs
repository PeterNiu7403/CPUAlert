using MoleWindows.Services;
using Xunit;

namespace MoleWindows.Tests;

public sealed class MoleWindowsStartupOptionsTests
{
    [Fact]
    public void Parse_DefaultsToNormalStartup()
    {
        var options = MoleWindowsStartupOptions.Parse(null, _ => null);

        Assert.False(options.ShowTrayHudDiagnostic);
        Assert.False(options.DisableTray);
        Assert.Null(options.InitialRoute);
    }

    [Fact]
    public void Parse_ReadsDiagnosticFlagsFromArguments()
    {
        var options = MoleWindowsStartupOptions.Parse("--show-tray-hud --no-tray", _ => null);

        Assert.True(options.ShowTrayHudDiagnostic);
        Assert.True(options.DisableTray);
    }

    [Fact]
    public void Parse_ReadsDiagnosticFlagsFromEnvironment()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["MOLEWINDOWS_SHOW_TRAY_HUD"] = "true",
            ["MOLEWINDOWS_DISABLE_TRAY"] = "1",
            ["MOLEWINDOWS_START_ROUTE"] = "purge"
        };

        var options = MoleWindowsStartupOptions.Parse(string.Empty, name => values.GetValueOrDefault(name));

        Assert.True(options.ShowTrayHudDiagnostic);
        Assert.True(options.DisableTray);
        Assert.Equal("purge", options.InitialRoute);
    }

    [Fact]
    public void Parse_ReadsInitialRouteFromArguments()
    {
        var options = MoleWindowsStartupOptions.Parse("--route=history", _ => null);

        Assert.Equal("history", options.InitialRoute);
    }
}
