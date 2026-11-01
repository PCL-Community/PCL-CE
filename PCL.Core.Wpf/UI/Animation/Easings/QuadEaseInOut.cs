namespace PCL.Core.UI.Animation.Easings;

public class QuadEaseInOut : Easing
{
    public static QuadEaseInOut Shared { get; } = new();

    protected override double EaseCore(double progress)
    {
        return progress < 0.5
            ? 2 * progress * progress
            : progress * (4 - 2 * progress) - 1;
    }
}