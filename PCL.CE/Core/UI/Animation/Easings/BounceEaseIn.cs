using PCL.CE.Core.Utils;

namespace PCL.CE.Core.UI.Animation.Easings;

public class BounceEaseIn : Easing
{
    protected override double EaseCore(double progress)
    {
        return 1 - EaseUtils.Bounce(1 - progress);
    }
}