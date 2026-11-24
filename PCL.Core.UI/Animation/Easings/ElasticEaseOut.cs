namespace PCL.Core.UI.Animation.Easings;

public class ElasticEaseOut(EasePower power = EasePower.Middle) : Easing
{
    private readonly int _p = (int)power + 4;

    public static ElasticEaseOut Shared { get; } = new();

    protected override double EaseCore(double progress)
    {
        // return Math.Sin(-EasingMath.ElasticPiTimes6Point5 * (progress + 1d))
        //     * Math.Exp(-EasingMath.ElasticLn2Times10 * progress)
        //     + 1d;
        var t = 1d - Math.Clamp(progress, 0d, 1d);
        return 1d
            - Math.Pow(t, (_p - 1) * 0.25d)
            * Math.Cos((_p - 3.5d) * Math.PI * Math.Pow(1d - t, 1.5d));
    }
}