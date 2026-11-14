namespace PCL.Core.UI.Animation.Easings;

public class ElasticEaseIn(EasePower power = EasePower.Middle) : Easing
{
    private readonly int _p = (int)power + 4;

    public static ElasticEaseIn Shared { get; } = new();

    protected override double EaseCore(double progress)
    {
        // return Math.Sin(EasingMath.ElasticPiTimes6Point5 * progress)
        //     * Math.Exp(EasingMath.ElasticLn2Times10 * (progress - 1d));
        var t = Math.Clamp(progress, 0d, 1d);
        return Math.Pow(t, (_p - 1) * 0.25d)
            * Math.Cos((_p - 3.5d) * Math.PI * Math.Pow(1d - t, 1.5d));
    }
}