namespace PCL.Core.UI.Animation.Easings;

public class CubicEaseIn : Easing
{
    protected override double EaseCore(double progress)
    {
        return progress * progress * progress;
    }
}