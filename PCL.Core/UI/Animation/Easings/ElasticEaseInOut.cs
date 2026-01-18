using System;
using PCL.Core.Utils;

namespace PCL.Core.UI.Animation.Easings;

public class ElasticEaseInOut : Easing
{
    protected override double EaseCore(double progress)
    {
        if (progress < 0.5d)
        {
            var t = progress * 2d;
            return 0.5d * Math.Sin(EaseUtils.ElasticPiTimes6Point5 * t) *
                   Math.Exp(EaseUtils.ElasticLn2Times10 * (t - 1d));
        }
        else
        {
            var t = progress * 2d - 1d;
            return 0.5d * (Math.Sin(-EaseUtils.ElasticPiTimes6Point5 * (t + 1d)) *
                Math.Exp(-EaseUtils.ElasticLn2Times10 * t) + 2d);
        }
    }
}