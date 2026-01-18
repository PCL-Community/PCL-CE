namespace PCL.Core.UI.Animation.Easings;

/// <summary>
/// 所有缓动类的基类。
/// </summary>
public abstract class Easing : IEasing
{
    protected abstract double EaseCore(double progress);

    public double Ease(double progress)
    {
        return progress switch
        {
            <= 0.0 => 0.0,
            >= 1.0 => 1.0,
            _ => EaseCore(progress)
        };
    }
    
    public double Ease(int currentFrame, int totalFrames)
    {
        return totalFrames <= 1 ? 1.0 : Ease((double)currentFrame / (totalFrames - 1));
    }
}