namespace PCL.Core.UI.Animation.Easings;

public class CubicEaseOut : Easing
{
    public static CubicEaseOut Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        var f = progress - 1;
        return f * f * f + 1;
    }
}