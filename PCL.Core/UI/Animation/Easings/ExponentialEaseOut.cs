using System;

namespace PCL.Core.UI.Animation.Easings;

public class ExponentialEaseOut : Easing
{
    public static ExponentialEaseOut Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        return Math.Abs(progress - 1.0) < 1e-4 ? progress : 1 - Math.Pow(2, -10 * progress);
    }
}