using System;

namespace PCL.Core.UI.Animation.Easings;

public class SineEaseInOut : Easing
{
    public static SineEaseInOut Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    { 
        return -(Math.Cos(Math.PI * progress) - 1) / 2;
    }
}