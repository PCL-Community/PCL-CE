namespace PCL.Core.UI.Animation.Easings;

public class QuarticEaseInOut : Easing
{
    protected override double EaseCore(double progress)
    {
        if (progress < 0.5d)
        {
            var p2 = progress * progress;
            return 8 * p2 * p2;
        }

        var f = progress - 1;
        var f2 = f * f;
        return -8 * f2 * f2 + 1;
    }
}