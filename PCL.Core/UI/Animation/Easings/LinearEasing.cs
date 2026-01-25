namespace PCL.Core.UI.Animation.Easings;

public class LinearEasing : Easing
{
    protected override double EaseCore(double progress)
    {
        return progress;
    }
}