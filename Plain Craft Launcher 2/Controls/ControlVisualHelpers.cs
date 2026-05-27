using System.Windows;

namespace PCL;

internal static class ControlVisualHelpers
{
    internal static bool ShouldAnimate(FrameworkElement control, object? animationOverride = null)
    {
        return control.IsLoaded && ModAnimation.AniControlEnabled == 0 && !false.Equals(animationOverride);
    }

    internal static void AnimateColorOrSetResource(FrameworkElement target, DependencyProperty property,
        string resourceKey, int duration, string animationKey, bool shouldAnimate)
    {
        if (shouldAnimate)
        {
            ModAnimation.AniStart(ModAnimation.AaColor(target, property, resourceKey, duration), animationKey);
        }
        else
        {
            ModAnimation.AniStop(animationKey);
            target.SetResourceReference(property, resourceKey);
        }
    }
}
