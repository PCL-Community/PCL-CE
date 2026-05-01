using System.Windows;
using PCL.CE.Core.UI.Animation.Animatable;
using PCL.CE.Core.UI.Animation.Core;
using PCL.CE.Core.UI.Animation.ValueProcessor;

namespace PCL.CE.Core.UI.Animation;

public class ThicknessFromToAnimation : FromToAnimationBase<Thickness>
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