using WinMoe.Models;
using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class LifetimeStatsAggregatorTests
{
    [Fact]
    public void Aggregate_SumsCleanBytesAndCountsOperations()
    {
        var entries = new[]
        {
            new OperationHistoryEntry(DateTimeOffset.UtcNow, "ui", "clean", "preview", 0, true, 10, "Freed 1073741824 bytes · 1 GB · 3 items"),
            new OperationHistoryEntry(DateTimeOffset.UtcNow, "ui", "clean", "preview", 0, true, 10, "Freed 512 MB preview"),
            new OperationHistoryEntry(DateTimeOffset.UtcNow, "local", "uninstall", "App", 0, true, 20, "Started uninstaller"),
            new OperationHistoryEntry(DateTimeOffset.UtcNow, "local", "optimize", "--dry-run", 0, true, 30, "Optimized"),
            new OperationHistoryEntry(DateTimeOffset.UtcNow, "local", "optimize", "--dry-run", 1, false, 30, "Failed"),
            new OperationHistoryEntry(DateTimeOffset.UtcNow, "local", "status", "", 0, true, 5, "ok")
        };

        var stats = LifetimeStatsAggregator.Aggregate(entries);

        Assert.Equal(2, stats.CleanOperations);
        Assert.Equal(1_073_741_824L + (512L * 1_048_576L), stats.CleanedBytes);
        Assert.Equal(1, stats.UninstallCount);
        Assert.Equal(1, stats.OptimizeCount);
        Assert.Equal("1.5 GB", stats.CleanedText);
        Assert.Equal("1", stats.UninstalledText);
        Assert.Equal("1", stats.OptimizedText);
    }

    [Fact]
    public void Aggregate_EmptyHistory_ShowsEmDash()
    {
        var stats = LifetimeStatsAggregator.Aggregate([]);
        Assert.Equal("—", stats.CleanedText);
        Assert.Equal("—", stats.UninstalledText);
        Assert.Equal("—", stats.OptimizedText);
    }
}
