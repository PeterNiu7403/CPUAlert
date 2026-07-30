using MoleWindows.Services;
using Xunit;

namespace MoleWindows.Tests;

public sealed class SystemMoleEngineProbeTests
{
    [Fact]
    public void BuildCandidatePaths_PrefersProcessDirectoryBeforeExtractionDirectory()
    {
        var processDirectory = Path.Combine("C:", "MoleWindows", "publish");
        var extractionDirectory = Path.Combine("C:", "Users", "Local", "Temp", ".net", "MoleWindows");

        var candidates = SystemMoleEngineProbe.BuildCandidatePaths([processDirectory, extractionDirectory]);

        Assert.Equal(Path.Combine(processDirectory, "Assets", "mo.exe"), candidates[0]);
        Assert.Equal(Path.Combine(processDirectory, "Assets", "Mole", "mo.exe"), candidates[1]);
        Assert.Contains(Path.Combine(extractionDirectory, "Assets", "Mole", "mo.cmd"), candidates);
        Assert.True(
            Array.IndexOf(candidates.ToArray(), Path.Combine(processDirectory, "Assets", "Mole", "mo.exe"))
            < Array.IndexOf(candidates.ToArray(), Path.Combine(extractionDirectory, "Assets", "Mole", "mo.cmd")));
    }
}
