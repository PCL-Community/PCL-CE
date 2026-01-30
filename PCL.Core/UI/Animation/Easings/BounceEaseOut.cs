using PCL.Core.Utils;

namespace PCL.Core.UI.Animation.Easings;

public class BounceEaseOut : Easing
{
    protected override double EaseCore(double progress)
    {
        return EaseUtils.Bounce(progress);
    }
}