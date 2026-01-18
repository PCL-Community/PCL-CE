namespace PCL.Core.UI.Animation.Easings;

public class QuadEaseOut : Easing
{
    protected override double EaseCore(double progress)
    {
        return 1 - (1 - progress) * (1 - progress);
    }
}