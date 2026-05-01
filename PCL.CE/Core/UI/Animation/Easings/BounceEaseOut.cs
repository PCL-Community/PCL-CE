using PCL.CE.Core.Utils;

namespace PCL.CE.Core.UI.Animation.Easings;

public class BounceEaseOut : Easing
{
    protected override double EaseCore(double progress)
    {
        return EaseUtils.Bounce(progress);
    }
}