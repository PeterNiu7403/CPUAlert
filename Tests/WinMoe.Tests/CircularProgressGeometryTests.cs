using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class CircularProgressGeometryTests
{
    [Fact]
    public void CreateDash_FullCircle_HasTinyGap()
    {
        var radius = 22d;
        var dash = CircularProgressGeometry.CreateDash(100, radius);
        Assert.InRange(dash.Filled, CircularProgressGeometry.Circumference(radius) - 0.01, CircularProgressGeometry.Circumference(radius) + 0.01);
        Assert.Equal(0.001d, dash.Gap);
    }

    [Fact]
    public void CreateDash_Empty_IsMostlyGap()
    {
        var dash = CircularProgressGeometry.CreateDash(0, 10);
        Assert.Equal(0, dash.Filled);
        Assert.True(dash.Gap > 50);
    }

    [Fact]
    public void CreateDash_Half_IsBalanced()
    {
        var radius = 10d;
        var half = CircularProgressGeometry.Circumference(radius) / 2;
        var dash = CircularProgressGeometry.CreateDash(50, radius);
        Assert.InRange(dash.Filled, half - 0.01, half + 0.01);
        Assert.InRange(dash.Gap, half - 0.01, half + 0.01);
    }
}
