namespace PCL.Core.UI.Animation.Easings;

public class QuadEaseOut : Easing
{
    public static QuadEaseOut Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        return 1 - (1 - progress) * (1 - progress);
    }
}