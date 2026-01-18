using System;

namespace PCL.Core.UI.Animation.Easings;

public class BackEaseIn : Easing
{
    public static BackEaseIn Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        return progress * (progress * progress - Math.Sin(progress * Math.PI)); 
    }
}