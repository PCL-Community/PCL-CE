namespace PCL.Core.UI.Animation.Easings;

public class LinearEasing : Easing
{
    public static LinearEasing Shared { get; } = new();
    
    protected override double EaseCore(double progress)
    {
        return progress;
    }
}