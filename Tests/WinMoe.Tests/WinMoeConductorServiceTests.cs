using System;
using System.IO;
using System.Linq;
using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

// Pins WinMoeConductorService's pure command shaping + resolution. The spawn itself can't run in
// CI (no bundled molewindows.exe), so these cover everything up to it; the envelope parse is covered by
// WinMoeEnvelopeTests. This mirrors the macOS envelope tests so both GUIs prove the same contract.
public class WinMoeConductorServiceTests
{
    [Fact]
    public void BuildArguments_AppendsJsonAfterPositionalArgs()
    {
        Assert.Equal(
            new[] { "analyze", @"C:\Users", "--json" },
            WinMoeConductorService.BuildArguments("analyze", new[] { @"C:\Users" }));
    }

    [Fact]
    public void BuildArguments_NoArgs_IsJustCommandPlusJson()
    {
        Assert.Equal(
            new[] { "status", "--json" },
            WinMoeConductorService.BuildArguments("status", Array.Empty<string>()));
    }

    [Fact]
    public void CandidateExecutablePaths_LookBesideTheApp_UnderAssets()
    {
        var paths = WinMoeConductorService
            .CandidateExecutablePaths(new[] { @"C:\App", @"C:\App" })
            .ToList();

        Assert.Single(paths); // duplicate base dir deduped (case-insensitive)
        Assert.EndsWith(Path.Combine("Assets", "molewindows.exe"), paths[0]);
    }

    [Fact]
    public void CandidateExecutablePaths_SkipsBlankBaseDirs()
    {
        var paths = WinMoeConductorService
            .CandidateExecutablePaths(new[] { "", "   ", @"C:\App" })
            .ToList();

        Assert.Single(paths);
    }

    // Safety-critical: the destructive confirm→apply mapping (molewindows defaults to dry-run).
    [Fact]
    public void ActionArguments_Confirmed_AddsApply()
    {
        // A confirmed (live) maintenance run MUST add --apply, or a "real" clean silently no-ops.
        Assert.Equal(new[] { "--apply" }, WinMoeConductorService.ActionArguments(confirm: true));
    }

    [Fact]
    public void ActionArguments_Unconfirmed_IsPreviewOnly()
    {
        // Unconfirmed → no --apply → molewindows's default dry-run (preview). Must NOT delete for real.
        Assert.Empty(WinMoeConductorService.ActionArguments(confirm: false));
    }
}
