using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class CleanupPathSafetyTests
{
    [Fact]
    public void IsConcreteDeletablePath_RejectsRelativeAndWindows()
    {
        Assert.False(OperationPlanValidator.IsConcreteDeletablePath("relative\\file.tmp"));
        Assert.False(OperationPlanValidator.IsConcreteDeletablePath(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
    }

    [Fact]
    public void IsConcreteDeletablePath_AcceptsExistingTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"winmoe-clean-test-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "ok");
        try
        {
            Assert.True(OperationPlanValidator.IsConcreteDeletablePath(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
