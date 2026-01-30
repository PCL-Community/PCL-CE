using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.Core;

namespace PCL.Core.UI.Animation;

public sealed class DoubleFromToAnimation : FromToAnimationBase<double>
{
    public override IAnimationFrame? ComputeNextFrame(IAnimatable target)
    {
        // 应用缓动函数
        var easedProgress = Easing.Ease(CurrentFrame, TotalFrames);

        // 计算当前值
        CurrentValue = ValueType == AnimationValueType.Relative
            ? From + To * easedProgress
            : From + (To - From) * easedProgress;

        return base.ComputeNextFrame(target);
    }
}