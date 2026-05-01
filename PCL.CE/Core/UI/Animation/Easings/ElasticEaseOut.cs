using System;
using PCL.CE.Core.Utils;

namespace PCL.CE.Core.UI.Animation.Easings;

public class ElasticEaseOut : Easing
{
    protected override double EaseCore(double progress)
    {
        return Math.Sin(-EaseUtils.ElasticPiTimes6Point5 * (progress + 1d)) *
            Math.Exp(-EaseUtils.ElasticLn2Times10 * progress) + 1d;
    }
}