using System;

namespace PCL.Core.UI.Animation.Easings;

public class SineEaseIn : Easing
{
    protected override double EaseCore(double progress)
    {
        return 1 - Math.Cos(progress * Math.PI / 2);
    }
}