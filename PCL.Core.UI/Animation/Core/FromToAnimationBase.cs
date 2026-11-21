using System.Diagnostics;
using PCL.Core.Logging;
using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.Easings;
using PCL.Core.UI.Animation.ValueProcessor;

namespace PCL.Core.UI.Animation.Core;

internal interface IClockSampledAnimation
{
    bool IsClockFinished { get; }
    void ArmClock(long startStamp);
}

public class FromToAnimationBase<T> : AnimationBase, IFromToAnimation, IClockSampledAnimation
{
    private bool _clockArmed;
    private long _clockStartStamp;
    private int _currentFrame;
    private bool _hasPreviousValue;
    private T? _previousValue;
    private T? _startValue;

    public IEasing Easing { get; set; } = new LinearEasing();
    public T From { get; set; } = default!;
    public bool HasFrom { get; set; }
    public T? To { get; set; }
    public AnimationValueType ValueType { get; set; } = AnimationValueType.Absolute;
    public TimeSpan Duration { get; set; }
    public TimeSpan Delay { get; set; }
    public T? CurrentValue { get; internal set; }

    internal bool IsClockFinished { get; private set; }

    void IClockSampledAnimation.ArmClock(long startStamp)
    {
        ArmClock(startStamp);
    }

    bool IClockSampledAnimation.IsClockFinished => IsClockFinished;

    object? IFromToAnimation.CurrentValue
    {
        get => CurrentValue;
        set
        {
            if (value is not T typed)
                throw new InvalidCastException($"无法将 {value!.GetType()} 转换为 {typeof(T)}");
            CurrentValue = typed;
        }
    }

    public int TotalFrames { get; private set; }

    public override int CurrentFrame
    {
        get => Interlocked.CompareExchange(ref _currentFrame, 0, 0);
        set => Interlocked.Exchange(ref _currentFrame, value);
    }


    public override Task<IAnimation> RunAsync(IAnimatable target)
    {
        var actualTarget = Target ?? target;
        var clone = Prepare(actualTarget);
        return clone.Status == AnimationStatus.Completed
            ? Task.FromResult<IAnimation>(clone)
            : RunDelayedAsync(clone, actualTarget);
    }

    public override IAnimation RunFireAndForget(IAnimatable target)
    {
        try
        {
            var actualTarget = Target ?? target;
            var clone = Prepare(actualTarget);
            if (clone.Status == AnimationStatus.Completed)
                return clone;

            var delay = AnimationService.ScaleDuration(clone.Delay);
            if (delay <= TimeSpan.Zero)
            {
                AnimationService.PushAnimationFireAndForget(clone, actualTarget);
                return clone;
            }

            AnimationTaskObserver.Observe(
                Task.Run(async () =>
                {
                    await Task.Delay(delay);
                    if (clone.Status != AnimationStatus.Canceled)
                        AnimationService.PushAnimationFireAndForget(clone, actualTarget);
                }),
                "延迟启动动画失败");

            return clone;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Animation", $"启动动画失败：{Name}");
            Status = AnimationStatus.Completed;
            return this;
        }
    }

    public override void Cancel()
    {
        Interlocked.Exchange(ref _currentFrame, TotalFrames);

        Status = AnimationStatus.Canceled;
    }

    public override IAnimationFrame? ComputeNextFrame(IAnimatable target)
    {
        if (_currentFrame < TotalFrames)
        {
            if (ValueType == AnimationValueType.Relative)
            {
                var current = CurrentValue!;
                var previous = _hasPreviousValue ? _previousValue! : From!;
                var delta = ValueProcessorManager.Subtract(current, previous);
                _previousValue = current;
                _hasPreviousValue = true;
                return new FromToAnimationFrame<T>(target, delta, default!, true);
            }

            return new FromToAnimationFrame<T>(
                target,
                ValueProcessorManager.Subtract(CurrentValue!, From!),
                From!);
        }

        Status = AnimationStatus.Completed;
        return null;
    }

    internal void ArmClock(long startStamp)
    {
        if (_clockArmed) return;

        _clockArmed = true;
        _clockStartStamp = startStamp;
    }

    /// <summary>
    /// 帧回调驱动时按墙钟进度采样，使任意刷新率下时长不变且每帧都有新值。 未武装时钟时仍按 <see cref="CurrentFrame" />
    /// 逐帧采样，供测试和手动驱动使用。
    /// </summary>
    protected double SampleEasedProgress()
    {
        if (!_clockArmed)
            return Easing.Ease(CurrentFrame, TotalFrames);

        if (IsClockFinished)
            return Easing.Ease(1d);

        var speed = AnimationService.Speed;
        if (speed is <= 0d or >= 200d || Duration <= TimeSpan.Zero)
        {
            IsClockFinished = true;
            return Easing.Ease(1d);
        }

        var span = Duration.TotalSeconds * speed;
        var elapsed = (Stopwatch.GetTimestamp() - _clockStartStamp)
            / (double)Stopwatch.Frequency;
        if (elapsed >= span)
        {
            IsClockFinished = true;
            return Easing.Ease(1d);
        }

        return Easing.Ease(elapsed / span);
    }

    private static async Task<IAnimation> RunDelayedAsync(
        FromToAnimationBase<T> animation,
        IAnimatable target)
    {
        var delay = AnimationService.ScaleDuration(animation.Delay);
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay);

        if (animation.Status != AnimationStatus.Canceled)
            await AnimationService.PushAnimationAsync(animation, target);

        return animation;
    }

    private FromToAnimationBase<T> Prepare(IAnimatable target)
    {
        AnimationService.EnsureStarted();

        FromToAnimationBase<T>? clone = null;

        if (AnimationService.UiAccessProvider.CheckAccess())
            PrepareOnUi();
        else
            AnimationService.UiAccessProvider.Invoke(PrepareOnUi);

        return clone!;

        void PrepareOnUi()
        {
            _RunCore(target);
            clone = (FromToAnimationBase<T>)MemberwiseClone();

            if (!AnimationService.IsInstant) return;

            var final = ValueType == AnimationValueType.Relative
                ? ValueProcessorManager.Add(_startValue!, To!)
                : To!;
            target.SetValue(final!);
            clone.Status = AnimationStatus.Completed;
            clone.RaiseCompleted();
        }
    }

    internal void PrepareForTest(IAnimatable target)
    {
        _RunCore(target);
    }

    private void _RunCore(IAnimatable target)
    {
        _currentFrame = 0;
        _hasPreviousValue = false;
        _previousValue = default;
        _clockArmed = false;
        IsClockFinished = false;
        _clockStartStamp = 0;

        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(To);

        var current = target.GetValue();
        _startValue = current is T typed
            ? typed
            : HasFrom
                ? From
                : ValueProcessorManager.DefaultValue<T>();

        if (!HasFrom)
            From = ValueType == AnimationValueType.Relative
                ? ValueProcessorManager.DefaultValue<T>()
                : _startValue;

        TotalFrames = Math.Max(
            1,
            (int)Math.Round(Duration.TotalSeconds * AnimationService.Fps * AnimationService.Speed));

        Status = AnimationStatus.Running;
    }
}