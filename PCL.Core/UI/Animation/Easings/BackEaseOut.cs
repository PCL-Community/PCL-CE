using System;

namespace PCL.Core.UI.Animation.Easings;

public class BackEaseOut : Easing
{
    public static BackEaseOut Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        var p = 1 - progress;
        return 1 - p * (p * p - Math.Sin(p * Math.PI));
    }
}