using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class DiskVolumeStatsTests
{
    [Fact]
    public void TryGetForPath_ReturnsUsage_ForExistingDriveRoot()
    {
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
        Assert.False(string.IsNullOrWhiteSpace(systemRoot));

        var usage = DiskVolumeStats.TryGetForPath(systemRoot!);

        Assert.NotNull(usage);
        Assert.True(usage!.TotalBytes > 0);
        Assert.InRange(usage.UsedBytes, 0, usage.TotalBytes);
        Assert.InRange(usage.UsagePercent, 0, 100);
        Assert.Contains('/', usage.UsedOverTotalText);
    }

    [Fact]
    public void TryGetForPath_ReturnsNull_ForBogusPath()
    {
        Assert.Null(DiskVolumeStats.TryGetForPath(@"Z:\definitely-missing-winmoe-volume-xyz"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildBreadcrumb_FallsBack_WhenPathMissing(string? path)
    {
        Assert.Equal("整盘 › 主目录", DiskVolumeStats.BuildBreadcrumb(path));
    }

    [Fact]
    public void BuildBreadcrumb_UsesProfileUserName()
    {
        var profile = Path.Combine(Path.GetTempPath(), "WinMoeTests", "profiles", "alice");
        Directory.CreateDirectory(profile);
        try
        {
            Assert.Equal("整盘 › alice", DiskVolumeStats.BuildBreadcrumb(profile, profile));
            Assert.Equal(
                "整盘 › alice › Documents",
                DiskVolumeStats.BuildBreadcrumb(Path.Combine(profile, "Documents"), profile));
            Assert.Equal(
                "整盘 › alice › a › b › c",
                DiskVolumeStats.BuildBreadcrumb(Path.Combine(profile, "a", "b", "c"), profile));
            // Depth cap: keep last 3 segments under user.
            Assert.Equal(
                "整盘 › alice › b › c › d",
                DiskVolumeStats.BuildBreadcrumb(Path.Combine(profile, "a", "b", "c", "d"), profile));
        }
        finally
        {
            try
            {
                Directory.Delete(Path.Combine(Path.GetTempPath(), "WinMoeTests", "profiles"), recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    [Fact]
    public void FormatHeaderMetrics_MatchesMoleLayout()
    {
        var volume = new DiskVolumeStats.VolumeUsage(
            UsedBytes: 487L * 1_073_741_824,
            TotalBytes: 994L * 1_073_741_824,
            UsagePercent: 49);

        var text = DiskVolumeStats.FormatHeaderMetrics("当前 252.86 GB", volume);

        Assert.StartsWith("当前 252.86 GB · 磁盘 ", text);
        Assert.Contains(" / ", text);
    }

    [Fact]
    public void FormatHeaderMetrics_PrefixesBareSize()
    {
        Assert.Equal("当前 10 GB · 磁盘 —", DiskVolumeStats.FormatHeaderMetrics("10 GB", null));
    }
}
