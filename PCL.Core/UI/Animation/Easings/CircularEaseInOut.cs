using System;

namespace PCL.Core.UI.Animation.Easings;

public class CircularEaseInOut : Easing
{
    public static CircularEaseInOut Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        if (progress < 0.5)
        {
            return 0.5 * (1 - Math.Sqrt(1 - 4 * progress * progress));
        }

        var t = 2 * progress;
        return 0.5 * (Math.Sqrt((3 - t) * (t - 1)) + 1);
    }
}