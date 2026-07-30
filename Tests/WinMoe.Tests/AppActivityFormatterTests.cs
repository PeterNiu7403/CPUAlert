using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class AppActivityFormatterTests
{
    [Fact]
    public void Format_PrefersRunningOverTimestamp()
    {
        var text = AppActivityFormatter.Format(
            DateTimeOffset.UtcNow.AddHours(-5),
            isRunningNow: true);
        Assert.Equal("使用中", text);
    }

    [Theory]
    [InlineData(20, "刚刚活跃")]
    [InlineData(90, "2 小时前")]
    [InlineData(60 * 36, "2 天前")]
    public void Format_UsesRelativeWindows(int minutesAgo, string expected)
    {
        var now = DateTimeOffset.Parse("2026-07-30T12:00:00Z");
        var text = AppActivityFormatter.Format(now.AddMinutes(-minutesAgo), false, now);
        Assert.Equal(expected, text);
    }

    [Fact]
    public void TryParseInstallDate_ReadsYyyyMmDd()
    {
        var date = AppActivityFormatter.TryParseInstallDate("20240615");
        Assert.NotNull(date);
        Assert.Equal(2024, date!.Value.Year);
        Assert.Equal(6, date.Value.Month);
        Assert.Equal(15, date.Value.Day);
    }

    [Fact]
    public void ResolveLastActivityUtc_UsesInstallDateFallback()
    {
        var date = AppActivityFormatter.ResolveLastActivityUtc(null, null, "20250101");
        Assert.NotNull(date);
        Assert.Equal(2025, date!.Value.Year);
    }
}
