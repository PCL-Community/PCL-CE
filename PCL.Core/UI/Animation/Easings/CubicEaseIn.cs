namespace PCL.Core.UI.Animation.Easings;

public class CubicEaseIn : Easing
{
    public static CubicEaseIn Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        return progress * progress * progress;
    }
}