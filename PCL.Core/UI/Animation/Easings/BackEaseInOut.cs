using System;

namespace PCL.Core.UI.Animation.Easings;

public class BackEaseInOut : Easing
{
    public static BackEaseInOut Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        if (progress < 0.5)
        {
            var f = 2 * progress;
            return 0.5 * f * (f * f - Math.Sin(f * Math.PI));
        }
        else
        {
            var f = 1 - (2 * progress - 1);
            return 0.5 * (1 - f * (f * f - Math.Sin(f * Math.PI))) + 0.5;
        }
    }
}