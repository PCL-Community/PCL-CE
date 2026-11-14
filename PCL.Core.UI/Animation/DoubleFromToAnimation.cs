using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.Core;

namespace PCL.Core.UI.Animation;

public class DoubleFromToAnimation : FromToAnimationBase<double>
{
    public override IAnimationFrame? ComputeNextFrame(IAnimatable target)
    {
        // 应用缓动函数
        var easedProgress = SampleEasedProgress();

        // 计算当前值
        CurrentValue = ValueType == AnimationValueType.Relative
            ? From + To * easedProgress
            : From + (To - From) * easedProgress;

        return base.ComputeNextFrame(target);
    }
}