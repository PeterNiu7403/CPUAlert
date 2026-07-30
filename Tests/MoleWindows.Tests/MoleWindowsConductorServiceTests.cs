using System;
using System.IO;
using System.Linq;
using MoleWindows.Services;
using Xunit;

namespace MoleWindows.Tests;

// Pins MoleWindowsConductorService's pure command shaping + resolution. The spawn itself can't run in
// CI (no bundled molewindows.exe), so these cover everything up to it; the envelope parse is covered by
// MoleWindowsEnvelopeTests. Mirrors the macOS MoleWindowsEnvelopeTests so both GUIs prove the same contract.
public class MoleWindowsConductorServiceTests
{
    [Fact]
    public void BuildArguments_AppendsJsonAfterPositionalArgs()
    {
        Assert.Equal(
            new[] { "analyze", @"C:\Users", "--json" },
            MoleWindowsConductorService.BuildArguments("analyze", new[] { @"C:\Users" }));
    }

    [Fact]
    public void BuildArguments_NoArgs_IsJustCommandPlusJson()
    {
        Assert.Equal(
            new[] { "status", "--json" },
            MoleWindowsConductorService.BuildArguments("status", Array.Empty<string>()));
    }

    [Fact]
    public void CandidateExecutablePaths_LookBesideTheApp_UnderAssets()
    {
        var paths = MoleWindowsConductorService
            .CandidateExecutablePaths(new[] { @"C:\App", @"C:\App" })
            .ToList();

        Assert.Single(paths); // duplicate base dir deduped (case-insensitive)
        Assert.EndsWith(Path.Combine("Assets", "molewindows.exe"), paths[0]);
    }

    [Fact]
    public void CandidateExecutablePaths_SkipsBlankBaseDirs()
    {
        var paths = MoleWindowsConductorService
            .CandidateExecutablePaths(new[] { "", "   ", @"C:\App" })
            .ToList();

        Assert.Single(paths);
    }

    // Safety-critical: the destructive confirm→apply mapping (molewindows defaults to dry-run).
    [Fact]
    public void ActionArguments_Confirmed_AddsApply()
    {
        // A confirmed (live) maintenance run MUST add --apply, or a "real" clean silently no-ops.
        Assert.Equal(new[] { "--apply" }, MoleWindowsConductorService.ActionArguments(confirm: true));
    }

    [Fact]
    public void ActionArguments_Unconfirmed_IsPreviewOnly()
    {
        // Unconfirmed → no --apply → molewindows's default dry-run (preview). Must NOT delete for real.
        Assert.Empty(MoleWindowsConductorService.ActionArguments(confirm: false));
    }
}
