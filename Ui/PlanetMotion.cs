using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;

namespace WinMoe.Ui;

/// <summary>
/// Optional slow planet spin for Clean/Optimize/Analyze heroes. Honors system Reduce Motion.
/// </summary>
public static class PlanetMotion
{
    public static bool IsMotionAllowed()
    {
        try
        {
            var settings = new UISettings();
            // AnimationsEnabled is false when Reduce Motion / related accessibility is on.
            return settings.AnimationsEnabled;
        }
        catch
        {
            return true;
        }
    }

    public static void StartSlowSpin(UIElement element, double secondsPerRevolution = 90)
    {
        if (element is null || !IsMotionAllowed())
        {
            return;
        }

        Stop(element);

        // Always pin the origin to the element center: without this an Ellipse
        // that declares its own RotateTransform in XAML rotates around its
        // top-left corner, which reads as drifting instead of spinning in place.
        element.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        if (element.RenderTransform is not RotateTransform rotate)
        {
            rotate = new RotateTransform();
            element.RenderTransform = rotate;
        }

        var animation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = new Duration(TimeSpan.FromSeconds(Math.Clamp(secondsPerRevolution, 30, 180))),
            RepeatBehavior = RepeatBehavior.Forever,
            EnableDependentAnimation = true
        };

        Storyboard.SetTarget(animation, rotate);
        Storyboard.SetTargetProperty(animation, "Angle");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        element.SetValue(MotionStoryboardProperty, storyboard);
        storyboard.Begin();
    }

    public static void Stop(UIElement element)
    {
        if (element.GetValue(MotionStoryboardProperty) is Storyboard existing)
        {
            existing.Stop();
            element.ClearValue(MotionStoryboardProperty);
        }
    }

    private static readonly DependencyProperty MotionStoryboardProperty =
        DependencyProperty.RegisterAttached(
            "MotionStoryboard",
            typeof(Storyboard),
            typeof(PlanetMotion),
            new PropertyMetadata(null));
}
