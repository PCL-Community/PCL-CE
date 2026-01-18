namespace PCL.Core.UI.Animation.Easings;

public class QuadEaseIn : Easing
{
    public static QuadEaseIn Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        return progress * progress;
    }
}