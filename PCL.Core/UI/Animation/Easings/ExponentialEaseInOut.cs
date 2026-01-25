using System;

namespace PCL.Core.UI.Animation.Easings;

public class ExponentialEaseInOut : Easing
{
    protected override double EaseCore(double progress)
    {
        if (progress < 0.5)
        {
            return 0.5 * Math.Pow(2, 20 * progress - 10);
        }

        return -0.5 * Math.Pow(2, -20 * progress + 10) + 1;
    }
}