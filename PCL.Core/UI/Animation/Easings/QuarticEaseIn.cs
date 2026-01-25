namespace PCL.Core.UI.Animation.Easings;

public class QuarticEaseIn : Easing
{
    protected override double EaseCore(double progress)
    {
        var p2 = progress * progress;
        return p2 * p2;  
    }
}