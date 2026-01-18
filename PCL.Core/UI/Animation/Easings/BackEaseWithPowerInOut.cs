using System;

namespace PCL.Core.UI.Animation.Easings;

public class BackEaseWithPowerInOut(EasePower power = EasePower.Middle) : Easing
{
    private readonly double _p = 3.0 - (double)power * 0.5;

    protected override double EaseCore(double progress)
    {
        var t = Math.Clamp(progress, 0.0, 1.0);

        if (t < 0.5)
        {
            var f = 2.0 * t;
            return 0.5 * (Math.Pow(f, _p) * Math.Cos(1.5 * Math.PI * (1.0 - f)));
        }
        else
        {
            var f = 2.0 * (t - 0.5);
            var inv = 1.0 - f;
            return 0.5 * (1.0 - Math.Pow(inv, _p) * Math.Cos(1.5 * Math.PI * f)) + 0.5;
        }
    }
}