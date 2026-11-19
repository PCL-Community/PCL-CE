namespace PCL.Core.UI.Animation.Easings;

public class FluentEaseIn(EasePower power = EasePower.Middle) : Easing
{
    protected override double EaseCore(double progress)
    {
        return Math.Pow(Math.Clamp(progress, 0d, 1d), (double)power);
    }
}

public class FluentEaseOut(EasePower power = EasePower.Middle) : Easing
{
    protected override double EaseCore(double progress)
    {
        return 1d - Math.Pow(Math.Clamp(1d - progress, 0d, 1d), (double)power);
    }
}

public class FluentEaseInOut(
    EasePower power = EasePower.Middle,
    double middle = 0.5d) : Easing
{
    private readonly FluentEaseIn _in = new(power);
    private readonly FluentEaseOut _out = new(power);

    protected override double EaseCore(double progress)
    {
        var split = Math.Clamp(middle, 0.00001d, 0.99999d);
        return progress < split
            ? split * _in.Ease(progress / split)
            : (1d - split) * _out.Ease((progress - split) / (1d - split)) + split;
    }
}

/// <summary>以指定初速度开始的平滑结束曲线。</summary>
public class FluentEaseOutWithInitial : Easing
{
    private readonly double _alpha;

    public FluentEaseOutWithInitial(
        double initialPixelPerSecond,
        double totalSecond,
        double totalDistance)
    {
        var v0Norm = initialPixelPerSecond * totalSecond / totalDistance;
        _alpha = Math.Max(0d, v0Norm - 1d);
    }

    protected override double EaseCore(double progress)
    {
        var p = Math.Clamp(progress, 0d, 1d);
        return _alpha == 0d ? p : (_alpha + 1d) * p / (1d + _alpha * p);
    }
}