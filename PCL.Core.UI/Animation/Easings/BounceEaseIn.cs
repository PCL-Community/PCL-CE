namespace PCL.Core.UI.Animation.Easings;

public class BounceEaseIn : Easing
{
    public static BounceEaseIn Shared { get; } = new();

    protected override double EaseCore(double progress)
    {
        return 1 - EasingMath.Bounce(1 - progress);
    }
}