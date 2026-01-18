namespace PCL.Core.UI.Animation.Easings;

public class QuarticEaseOut : Easing
{
    public static QuarticEaseOut Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        var f = progress - 1;
        var f2 = f * f;
        return -f2 * f2 + 1;
    }
}