namespace PCL.Core.UI.Animation.Easings;

public class QuarticEaseOut : Easing
{
    protected override double EaseCore(double progress)
    {
        var f = progress - 1;
        var f2 = f * f;
        return -f2 * f2 + 1;
    }
}