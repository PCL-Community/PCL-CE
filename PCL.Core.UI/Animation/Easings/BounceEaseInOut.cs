namespace PCL.Core.UI.Animation.Easings;

public class BounceEaseInOut : Easing
{
    public static BounceEaseInOut Shared { get; } = new();

    protected override double EaseCore(double progress)
    {
        return progress < 0.5
            ? 0.5 * (1 - EasingMath.Bounce(1 - progress * 2))
            : 0.5 * EasingMath.Bounce(progress * 2 - 1) + 0.5;
    }
}