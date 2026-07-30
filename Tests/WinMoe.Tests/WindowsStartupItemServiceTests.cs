using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class WindowsStartupItemServiceTests
{
    [Fact]
    public async Task GetStartupItemsAsync_ReturnsWithoutThrowing()
    {
        var service = new WindowsStartupItemService();
        var items = await service.GetStartupItemsAsync();

        Assert.NotNull(items);
        // Machine-dependent; just ensure distinct sources are well-formed when present.
        foreach (var item in items)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Name));
            Assert.False(string.IsNullOrWhiteSpace(item.Source));
        }
    }

    [Fact]
    public void ShortCommand_TruncatesLongPaths()
    {
        var longPath = @"C:\Program Files\Example\VeryLongApplicationNameThatShouldTruncate.exe " + new string('x', 80);
        var item = new StartupItem("Example", longPath, "Run", "当前用户 · 运行");
        Assert.True(item.ShortCommand.Length <= 64);
        Assert.EndsWith("…", item.ShortCommand);
    }
}
