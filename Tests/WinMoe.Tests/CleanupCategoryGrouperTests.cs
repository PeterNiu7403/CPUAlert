using WinMoe.Models;
using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class CleanupCategoryGrouperTests
{
    [Fact]
    public void Group_OrdersByTotalBytes_AndAggregatesSelection()
    {
        var items = new[]
        {
            new CleanupPreviewItem("Browser Caches", @"C:\a", "1.5GB", 1_500_000_000, 12),
            new CleanupPreviewItem("Browser Caches", @"C:\b", "512KB", 512_000, 1) { IsSelected = false },
            new CleanupPreviewItem("Developer Tools", @"C:\npm", "25MB", 25_000_000, 4),
            new CleanupPreviewItem("User Temp", @"C:\t", "1MB", 1_000_000, null)
        };

        var groups = CleanupCategoryGrouper.Group(items);

        Assert.Equal(3, groups.Count);
        Assert.Equal("Browser Caches", groups[0].Category);
        Assert.Equal(2, groups[0].Items.Count);
        Assert.Equal(1_500_512_000, groups[0].TotalBytes);
        Assert.Equal(1, groups[0].SelectedCount);
        Assert.Equal("Developer Tools", groups[1].Category);
        Assert.Equal("User Temp", groups[2].Category);
    }

    [Fact]
    public void Group_UsesCleanupFallback_WhenCategoryBlank()
    {
        var items = new[]
        {
            new CleanupPreviewItem("  ", @"C:\x", "1KB", 1024, null),
            new CleanupPreviewItem(string.Empty, @"C:\y", "2KB", 2048, null)
        };

        var groups = CleanupCategoryGrouper.Group(items);

        var group = Assert.Single(groups);
        Assert.Equal("Cleanup", group.Category);
        Assert.Equal(2, group.Items.Count);
        Assert.Equal(3072, group.TotalBytes);
    }

    [Fact]
    public void Group_IsEmpty_WhenNoItems()
    {
        Assert.Empty(CleanupCategoryGrouper.Group([]));
    }
}
