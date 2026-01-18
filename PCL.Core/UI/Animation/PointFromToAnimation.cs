using System.Windows;
using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.Core;
using PCL.Core.UI.Animation.ValueProcessor;

namespace PCL.Core.UI.Animation;

public class PointFromToAnimation : FromToAnimationBase<Point>
{
    public override IAnimationFrame? ComputeNextFrame(IAnimatable target)
    {
        // 应用缓动函数
        var easedProgress = Easing.Ease(CurrentFrame, TotalFrames);

        // 计算当前值
        CurrentValue = ValueType == AnimationValueType.Relative
            ? ValueProcessorManager.Add(From!.Value, ValueProcessorManager.Scale(To, easedProgress))
            : ValueProcessorManager.Add(From!.Value,
                ValueProcessorManager.Scale(ValueProcessorManager.Subtract(To, From!.Value), easedProgress));

        return base.ComputeNextFrame(target);
    }
}