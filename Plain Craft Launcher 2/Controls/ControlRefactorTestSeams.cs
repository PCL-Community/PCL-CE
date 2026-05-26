using System.Windows;

namespace PCL;

/// <summary>
/// Internal, behavior-neutral seams for deterministic verification of refactor-adjacent control logic.
/// </summary>
internal static class ControlRefactorTestSeams
{
    internal static bool ShouldAnimate(bool isLoaded, int aniControlEnabled, object? animationOverride = null)
    {
        return isLoaded && aniControlEnabled == 0 && !false.Equals(animationOverride);
    }

    internal static bool ShouldAnimate(FrameworkElement control, object? animationOverride = null)
    {
        return ShouldAnimate(control.IsLoaded, ModAnimation.AniControlEnabled, animationOverride);
    }
}
