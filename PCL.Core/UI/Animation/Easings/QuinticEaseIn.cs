namespace PCL.Core.UI.Animation.Easings;

public class QuinticEaseIn : Easing
{
    protected override double EaseCore(double progress)
    {
        var p2 = progress * progress;
        return p2 * p2 * progress;
    }
}