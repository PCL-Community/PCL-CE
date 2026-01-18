using System.ComponentModel;
using PCL.Core.UI.Converters;

namespace PCL.Core.UI.Animation.Easings;

/// <summary>
/// 定义了缓动类的接口。
/// </summary>
[TypeConverter(typeof(EasingConverter))]
public interface IEasing
{
    /// <summary>
    /// 返回指定进度的过渡值。
    /// </summary>
    /// <param name="progress">从 0.0 到 1.0 的进度值。</param>
    /// <returns>过渡值。</returns>
    double Ease(double progress);

    /// <summary>
    /// 返回指定动画帧的过渡值。
    /// </summary>
    /// <param name="currentFrame">当前动画帧。</param>
    /// <param name="totalFrames">总动画帧数量。</param>
    /// <returns>过渡值。</returns>
    double Ease(int currentFrame, int totalFrames);
}