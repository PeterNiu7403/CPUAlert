using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class DisplaySleepPreventionTests
{
    [Fact]
    public void FormatRemaining_ReturnsHoursAndMinutes()
    {
        Assert.Equal("剩余 3:59", DisplaySleepPreventionService.FormatRemaining(TimeSpan.FromHours(4) - TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void FormatRemaining_UnderOneHour_ShowsMinutes()
    {
        Assert.Equal("剩余 42 分钟", DisplaySleepPreventionService.FormatRemaining(TimeSpan.FromMinutes(42)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void FormatRemaining_ZeroOrNegative_ReturnsNull(int seconds)
    {
        Assert.Null(DisplaySleepPreventionService.FormatRemaining(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void FormatRemaining_Null_ReturnsNull()
    {
        Assert.Null(DisplaySleepPreventionService.FormatRemaining(null));
    }

    [Fact]
    public void Service_StopWhenInactive_StaysInactive()
    {
        using var service = new DisplaySleepPreventionService();
        service.Stop();
        Assert.False(service.IsActive);
        Assert.Null(service.ActiveDuration);
        Assert.Null(service.Remaining);
    }

    [Fact]
    public void Service_PreventForTimed_TracksDurationAndRemaining()
    {
        using var service = new DisplaySleepPreventionService();
        service.PreventFor(TimeSpan.FromHours(2));

        Assert.True(service.IsActive);
        Assert.Equal(TimeSpan.FromHours(2), service.ActiveDuration);
        Assert.InRange(service.Remaining ?? TimeSpan.Zero, TimeSpan.FromHours(1.9), TimeSpan.FromHours(2));

        service.Stop();
        Assert.False(service.IsActive);
        Assert.Null(service.ActiveDuration);
    }

    [Fact]
    public void Service_PreventForIndefinite_HasNoRemaining()
    {
        using var service = new DisplaySleepPreventionService();
        service.PreventFor(null);

        Assert.True(service.IsActive);
        Assert.Null(service.ActiveDuration);
        Assert.Null(service.Remaining);

        service.Stop();
        Assert.False(service.IsActive);
    }
}
