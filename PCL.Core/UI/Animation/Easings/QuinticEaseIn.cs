namespace PCL.Core.UI.Animation.Easings;

public class QuinticEaseIn : Easing
{
    public static QuinticEaseIn Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        var p2 = progress * progress;
        return p2 * p2 * progress;
    }
}