using System;

namespace PCL.Core.UI.Animation.Easings;

public class CircularEaseOut : Easing
{
    public static CircularEaseOut Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        return Math.Sqrt((2d - progress) * progress);
    }
}