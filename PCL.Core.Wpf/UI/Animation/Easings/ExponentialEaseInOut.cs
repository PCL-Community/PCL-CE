using System;

namespace PCL.Core.UI.Animation.Easings;

public class ExponentialEaseInOut : Easing
{
    public static ExponentialEaseInOut Shared { get; } = new();

    protected override double EaseCore(double progress)
    {
        return progress < 0.5
            ? 0.5 * Math.Pow(2, 20 * progress - 10)
            : -0.5 * Math.Pow(2, -20 * progress + 10) + 1;
    }
}