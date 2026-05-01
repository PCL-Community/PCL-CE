using System;

namespace PCL.CE.Core.UI.Animation.Easings;

public class SineEaseOut : Easing
{
    protected override double EaseCore(double progress)
    {
        return Math.Sin(progress * Math.PI / 2);
    }
}