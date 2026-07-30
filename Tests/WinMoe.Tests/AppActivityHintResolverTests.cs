using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class AppActivityHintResolverTests
{
    [Fact]
    public void ResolveForApps_MarksMatchingNamesAsActive()
    {
        var hints = AppActivityHintResolver.ResolveForApps(
            ["Visual Studio Code", "Stash", "Idle App"],
            ["Code", "stash", "explorer"]);

        Assert.Equal("使用中", hints["Visual Studio Code"]);
        Assert.Equal("使用中", hints["Stash"]);
        Assert.False(hints.ContainsKey("Idle App"));
    }

    [Fact]
    public void IsRunning_RequiresMeaningfulTokenLength()
    {
        var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ab", "chrome" };
        Assert.False(AppActivityHintResolver.IsRunning("AB Utility", running));
        Assert.True(AppActivityHintResolver.IsRunning("Google Chrome", running));
    }
}
