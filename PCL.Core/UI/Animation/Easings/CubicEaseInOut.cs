namespace PCL.Core.UI.Animation.Easings;

public class CubicEaseInOut : Easing
{
    public static CubicEaseInOut Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        if (progress < 0.5)
        {
            return 4 * progress * progress * progress;
        }

        var f = 2 * (progress - 1);
        return 0.5 * f * f * f + 1;
    }
}