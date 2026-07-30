using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class ShellPathActionsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinMoeTests", Guid.NewGuid().ToString("N"));

    public ShellPathActionsTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void CanOpen_True_ForExistingDirectory()
    {
        Assert.True(ShellPathActions.CanOpen(_root));
    }

    [Fact]
    public void CanOpen_False_ForMissingPath()
    {
        Assert.False(ShellPathActions.CanOpen(Path.Combine(_root, "missing-folder")));
    }

    [Fact]
    public void CanSendToRecycleBin_AllowsUserTempFile()
    {
        var file = Path.Combine(_root, "sample.txt");
        File.WriteAllText(file, "x");
        Assert.True(ShellPathActions.CanSendToRecycleBin(file));
    }

    [Fact]
    public void CanSendToRecycleBin_BlocksWindowsRootishPaths()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Assert.False(ShellPathActions.CanSendToRecycleBin(windows));
        Assert.False(ShellPathActions.CanSendToRecycleBin(Path.GetPathRoot(windows)));
    }

    [Fact]
    public void Normalize_ExpandsAndFullyQualifies()
    {
        var full = ShellPathActions.Normalize(_root);
        Assert.True(Path.IsPathFullyQualified(full));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
        }
    }
}
