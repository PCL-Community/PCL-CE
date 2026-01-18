using System;

namespace PCL.Core.UI.Animation.Easings;

public class SineEaseIn : Easing
{
    public static SineEaseIn Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        return 1 - Math.Cos(progress * Math.PI / 2);
    }
}