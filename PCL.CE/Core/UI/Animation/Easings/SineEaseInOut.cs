using System;

namespace PCL.CE.Core.UI.Animation.Easings;

public class SineEaseInOut : Easing
{
    protected override double EaseCore(double progress)
    { 
        return -(Math.Cos(Math.PI * progress) - 1) / 2;
    }
}