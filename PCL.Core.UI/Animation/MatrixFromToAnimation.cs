using Avalonia;
using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.Core;
using PCL.Core.UI.Animation.ValueProcessor;

namespace PCL.Core.UI.Animation;

public class MatrixFromToAnimation : FromToAnimationBase<Matrix>
{
    public override IAnimationFrame? ComputeNextFrame(IAnimatable target)
    {
        // 应用缓动函数
        var easedProgress = SampleEasedProgress();

        // 计算当前值
        CurrentValue = ValueType == AnimationValueType.Relative
            ? ValueProcessorManager.Add(From, ValueProcessorManager.Scale(To, easedProgress))
            : ValueProcessorManager.Add(
                From,
                ValueProcessorManager.Scale(
                    ValueProcessorManager.Subtract(To, From),
                    easedProgress));

        return base.ComputeNextFrame(target);
    }
}