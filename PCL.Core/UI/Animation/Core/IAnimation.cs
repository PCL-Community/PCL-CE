using System;
using System.Threading.Tasks;
using PCL.Core.UI.Animation.Animatable;

namespace PCL.Core.UI.Animation.Core;

public interface IAnimation
{
    /// <summary>
    /// 动画名。
    /// </summary>
    string Name { get; set; }
    /// <summary>
    /// 当前动画状态。
    /// </summary>
    AnimationStatus Status { get; }
    /// <summary>
    /// 当前动画帧索引。
    /// </summary>
    int CurrentFrame { get; set; }
    /// <summary>
    /// 异步方式运行动画。
    /// </summary>
    /// <param name="target">被动画的对象。</param>
    /// <returns>返回表示异步动画操作的任务。</returns>
    Task<IAnimation> RunAsync(IAnimatable target);
    /// <summary>
    /// 一发即忘方式运行动画。
    /// </summary>
    /// <param name="target">被动画的对象。</param>
    IAnimation RunFireAndForget(IAnimatable target);
    /// <summary>
    /// 取消动画。
    /// </summary>
    void Cancel();
    /// <summary>
    /// 计算下一帧。
    /// </summary>
    /// <param name="target">被动画的对象。</param>
    /// <returns>动画帧。</returns>
    IAnimationFrame? ComputeNextFrame(IAnimatable target);
    /// <summary>
    /// 触发动画开始事件。
    /// </summary>
    void RaiseStarted();
    /// <summary>
    /// 触发动画完成事件。
    /// </summary>
    void RaiseCompleted(); 
    /// <summary>
    /// 动画开始时触发的事件。
    /// </summary>
    event EventHandler Started;
    /// <summary>
    /// 动画完成时触发的事件。
    /// </summary>
    event EventHandler Completed;
}