namespace WinMoe.Services;

/// <summary>
/// Math for circular progress rings (Status battery card StrokeDashArray).
/// </summary>
public static class CircularProgressGeometry
{
    public readonly record struct Dash(double Filled, double Gap);

    public static Dash CreateDash(double percent, double radius)
    {
        var circumference = Circumference(radius);
        var filled = Math.Clamp(percent, 0d, 100d) / 100d * circumference;
        var gap = Math.Max(0.001d, circumference - filled);
        return new Dash(filled, gap);
    }

    public static double Circumference(double radius) => 2d * Math.PI * Math.Max(1d, radius);
}
