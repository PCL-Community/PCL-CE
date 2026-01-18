using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.UI.Animation.Easings;

/// <summary>
/// 复合缓动，支持多个缓动混合，每个缓动可独立设置时长，延迟和权重。
/// </summary>
public class CompositeEasing : Easing
{
    private readonly List<(IEasing easing, TimeSpan duration, TimeSpan delay, double weight)> _easings;
    private readonly TimeSpan _totalDuration;

    /// <summary>
    /// 初始化复合缓动。
    /// </summary>
    /// <param name="easings">参数元组：(缓动逻辑, 持续时长)</param>
    public CompositeEasing(params (IEasing easing, TimeSpan duration)[] easings)
        : this(easings.Select(e => (e.easing, e.duration, TimeSpan.Zero, 1.0 / (easings.Length > 0 ? easings.Length : 1))).ToArray())
    {
    }
    
    /// <summary>
    /// 初始化复合缓动。
    /// </summary>
    /// <param name="easings">参数元组：(缓动逻辑, 持续时长, 延迟时间)</param>
    public CompositeEasing(params (IEasing easing, TimeSpan duration, TimeSpan delay)[] easings)
        : this(easings.Select(e => (e.easing, e.duration, e.delay, 1.0 / (easings.Length > 0 ? easings.Length : 1))).ToArray())
    {
    }

    /// <summary>
    /// 初始化复合缓动。
    /// </summary>
    /// <param name="easings">参数元组：(缓动逻辑, 持续时长, 权重)</param>
    public CompositeEasing(params (IEasing easing, TimeSpan duration, double weight)[] easings)
        : this(easings.Select(e => (e.easing, e.duration, TimeSpan.Zero, e.weight)).ToArray())
    {
    }

    /// <summary>
    /// 初始化复合缓动。
    /// </summary>
    /// <param name="easings">参数元组：(缓动逻辑, 持续时长, 延迟时间, 权重)</param>
    public CompositeEasing(params (IEasing easing, TimeSpan duration, TimeSpan delay, double weight)[] easings)
    {
        if (easings == null || easings.Length == 0)
            throw new ArgumentException("至少需要一个缓动", nameof(easings));

        _easings = new List<(IEasing, TimeSpan, TimeSpan, double)>(easings.Length);
        
        long maxTicks = 0;

        foreach (var (easing, duration, delay, weight) in easings)
        {
            if (easing is null) throw new ArgumentNullException(nameof(easing));
            if (duration <= TimeSpan.Zero) throw new ArgumentException("duration 必须大于 zero");

            _easings.Add((easing, duration, delay, weight));

            long endTicks = (delay + duration).Ticks;
            if (endTicks > maxTicks) 
                maxTicks = endTicks;
        }
        
        _totalDuration = TimeSpan.FromTicks(maxTicks);
    }

    public TimeSpan TotalDuration => _totalDuration;

    protected override double EaseCore(double progress)
    {
        // 计算当前绝对时间
        var elapsed = _totalDuration * progress;

        var value = 0.0;

        foreach (var (easing, duration, delay, weight) in _easings)
        {
            if (weight == 0) continue;

            // 计算相对于该缓动的时间
            var localElapsed = elapsed - delay;

            double easingValue;

            // 还没开始
            if (localElapsed <= TimeSpan.Zero)
            {
                easingValue = 0.0; 
            }
            // 已经结束
            else if (localElapsed >= duration)
            {
                // 保持最终状态
                easingValue = easing.Ease(1.0);
            }
            // 正在运行
            else
            {
                // 避免除法浮点数计算问题
                var localProgress = localElapsed.TotalSeconds / duration.TotalSeconds;
                easingValue = easing.Ease(localProgress);
            }

            value += easingValue * weight;
        }

        return value;
    }
}