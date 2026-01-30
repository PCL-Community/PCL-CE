namespace PCL.Core.UI.Animation.Easings;

public class QuadEaseIn : Easing
{
    protected override double EaseCore(double progress)
    {
        return progress * progress;
    }
}