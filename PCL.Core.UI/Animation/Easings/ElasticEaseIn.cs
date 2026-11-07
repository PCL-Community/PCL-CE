namespace PCL.Core.UI.Animation.Easings;

public class ElasticEaseIn : Easing
{
    public static ElasticEaseIn Shared { get; } = new();

    protected override double EaseCore(double progress)
    {
        return Math.Sin(EasingMath.ElasticPiTimes6Point5 * progress)
            * Math.Exp(EasingMath.ElasticLn2Times10 * (progress - 1d));
    }
}