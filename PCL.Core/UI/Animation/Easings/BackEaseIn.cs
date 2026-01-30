using System;

namespace PCL.Core.UI.Animation.Easings;

public class BackEaseIn : Easing
{
    protected override double EaseCore(double progress)
    {
        return progress * (progress * progress - Math.Sin(progress * Math.PI)); 
    }
}