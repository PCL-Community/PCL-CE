using System;

namespace PCL.Core.UI.Animation.Easings;

public class CircularEaseIn : Easing
{
    protected override double EaseCore(double progress)
    {
        return 1 - Math.Sqrt(1d - progress * progress);
    }
}