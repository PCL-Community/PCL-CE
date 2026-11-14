using System.Diagnostics;
using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.Easings;
using PCL.Core.UI.Animation.ValueProcessor;

namespace PCL.Core.UI.Animation.Core;

internal interface IClockSampledAnimation
{
    void ArmClock(long startStamp);

    bool IsClockFinished { get; }
}

public class FromToAnimationBase<T> : AnimationBase, IFromToAnimation, IClockSampledAnimation
{
    private int _currentFrame;

    private bool _hasPreviousValue;
    private T? _previousValue;
    private T? _startValue;
    private bool _clockArmed;
    private bool _clockFinished;
    private long _clockStartStamp;

    public IEasing Easing { get; set; } = new LinearEasing();
    public T From { get; set; } = default!;
    public bool HasFrom { get; set; }
    public T? To { get; set; }
    public AnimationValueType ValueType { get; set; } = AnimationValueType.Absolute;
    public TimeSpan Duration { get; set; }
    public TimeSpan Delay { get; set; }
    public T? CurrentValue { get; internal set; }

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
    internal void ArmClock(long startStamp)
    {
        if (_clockArmed) return;

        _clockArmed = true;
        _clockStartStamp = startStamp;
    }

    internal bool IsClockFinished => _clockFinished;

    void IClockSampledAnimation.ArmClock(long startStamp)
    {
        ArmClock(startStamp);
    }

    bool IClockSampledAnimation.IsClockFinished => _clockFinished;

    /// <summary>
    ///     帧回调驱动时按墙钟进度采样，使任意刷新率下时长不变且每帧都有新值。
    ///     未武装时钟时仍按 <see cref="CurrentFrame" /> 逐帧采样，供测试和手动驱动使用。
    /// </summary>
    protected double SampleEasedProgress()
    {
        if (!_clockArmed)
            return Easing.Ease(CurrentFrame, TotalFrames);

        if (_clockFinished)
            return Easing.Ease(1d);

        var speed = AnimationService.Speed;
        if (speed is <= 0d or >= 200d || Duration <= TimeSpan.Zero)
        {
            _clockFinished = true;
            return Easing.Ease(1d);
        }

        // 与原逐帧公式一致：墙钟时长 = Duration * Speed。Speed 为 1 时等于设定时长。
        var span = Duration.TotalSeconds * speed;
        var elapsed = (Stopwatch.GetTimestamp() - _clockStartStamp)
            / (double)Stopwatch.Frequency;
        if (elapsed >= span)
        {
            _clockFinished = true;
            return Easing.Ease(1d);
        }

        return Easing.Ease(elapsed / span);
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

        _ = Task.Run(async () =>
        {
            await Task.Delay(delay);
            if (clone.Status != AnimationStatus.Canceled)
                AnimationService.PushAnimationFireAndForget(clone, actualTarget);
        });

        return clone;
    }

    public override void Cancel()
    {
        // 确保正常结束
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
        // 重置当前帧与相对动画的上一帧状态
        _currentFrame = 0;
        _hasPreviousValue = false;
        _previousValue = default;
        _clockArmed = false;
        _clockFinished = false;
        _clockStartStamp = 0;

        // 空值检查
        ArgumentNullException.ThrowIfNull(To);

        // 记录初始值
        _startValue = (T)target.GetValue()!;

        // 未显式提供 From 时，绝对动画从当前值开始，相对动画从默认值开始。
        if (!HasFrom)
            From = ValueType == AnimationValueType.Relative
                ? ValueProcessorManager.DefaultValue<T>()
                : _startValue;

        TotalFrames = Math.Max(
            1,
            (int)Math.Round(Duration.TotalSeconds * AnimationService.Fps * AnimationService.Speed));

        // 设置状态
        Status = AnimationStatus.Running;
    }
}