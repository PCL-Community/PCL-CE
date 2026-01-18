using System;

namespace PCL.Core.UI.Animation.Easings;

public class ExponentialEaseIn : Easing
{
    public static ExponentialEaseIn Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        return progress == 0 ? progress : Math.Pow(2, 10 * (progress - 1));
    }
}