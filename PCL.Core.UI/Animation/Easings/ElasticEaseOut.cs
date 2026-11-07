namespace PCL.Core.UI.Animation.Easings;

public class ElasticEaseOut : Easing
{
    public static ElasticEaseOut Shared { get; } = new();

    protected override double EaseCore(double progress)
    {
        return Math.Sin(-EasingMath.ElasticPiTimes6Point5 * (progress + 1d))
            * Math.Exp(-EasingMath.ElasticLn2Times10 * progress)
            + 1d;
    }
}