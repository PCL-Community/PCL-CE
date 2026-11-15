using System.Windows;

namespace PCL;

internal static class ControlVisualHelpers
{
    internal static bool ShouldAnimate(FrameworkElement control, object? animationOverride = null)
    {
        return control.IsLoaded && !Animations.AniExitsAnyControl() &&
               !false.Equals(animationOverride);
    }

    internal static void AnimateColorOrSetResource(FrameworkElement target, DependencyProperty property,
        string resourceKey, int duration, string animationKey, bool shouldAnimate)
    {
        if (shouldAnimate)
        {
            Animations.AniStart(Animations.AaColor(target, property, resourceKey, duration),
                animationKey);
        }
        else
        {
            Animations.AniStop(animationKey);
            target.SetResourceReference(property, resourceKey);
        }
    }
}
