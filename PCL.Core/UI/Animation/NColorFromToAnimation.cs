using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.Core;

namespace PCL.Core.UI.Animation;

public class NColorFromToAnimation : FromToAnimationBase<NColor>
{
    public override IAnimationFrame? ComputeNextFrame(IAnimatable target)
    {
        // 应用缓动函数
        var easedProgress = Easing.Ease(CurrentFrame, TotalFrames);
        
        // 计算当前值
        CurrentValue = ValueType == AnimationValueType.Relative
            ? From + To * (float)easedProgress
            : From + (To - From) * (float)easedProgress;
        
        return base.ComputeNextFrame(target);
    }
}