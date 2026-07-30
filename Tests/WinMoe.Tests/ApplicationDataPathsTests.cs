using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class ApplicationDataPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WinMoeDataPathsTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ResolveFile_UsesCurrentDirectoryForNewData()
    {
        var path = ApplicationDataPaths.ResolveFile(_root, "settings.json");

        Assert.Equal(Path.Combine(_root, "WinMoe", "settings.json"), path);
    }

    [Fact]
    public void ResolveFile_CopiesLegacyDataToCurrentDirectory()
    {
        var legacyPath = Path.Combine(_root, "MoleWindows", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, """{"TrayIconEnabled":false}""");

        var path = ApplicationDataPaths.ResolveFile(_root, "settings.json");

        Assert.Equal(Path.Combine(_root, "WinMoe", "settings.json"), path);
        Assert.Equal(File.ReadAllText(legacyPath), File.ReadAllText(path));
    }

    [Fact]
    public void ResolveFile_PrefersExistingCurrentData()
    {
        var currentPath = Path.Combine(_root, "WinMoe", "history.jsonl");
        var legacyPath = Path.Combine(_root, "MoleWindows", "history.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(currentPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(currentPath, "current");
        File.WriteAllText(legacyPath, "legacy");

        var path = ApplicationDataPaths.ResolveFile(_root, "history.jsonl");

        Assert.Equal(currentPath, path);
        Assert.Equal("current", File.ReadAllText(path));
    }

    [Fact]
    public void CurrentFile_DoesNotMigrateDiagnosticData()
    {
        var legacyPath = Path.Combine(_root, "MoleWindows", "startup.log");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, "stale route evidence");

        var path = ApplicationDataPaths.CurrentFile(_root, "startup.log");

        Assert.Equal(Path.Combine(_root, "WinMoe", "startup.log"), path);
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nested/settings.json")]
    [InlineData(@"nested\settings.json")]
    public void ResolveFile_RejectsInvalidFileNames(string fileName)
    {
        Assert.Throws<ArgumentException>(() => ApplicationDataPaths.ResolveFile(_root, fileName));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
