using PCL.Core.UI.Animation.Animatable;

namespace PCL.Core.UI.Animation.Core;

/// <summary>
///     用于在动画系统中执行 Action。
/// </summary>
public class ActionAnimation : AnimationBase
{
    private int _called;

    private CancellationTokenSource? _cts = new();
    private TaskCompletionSource? _tcs;

    public ActionAnimation()
    {
    }

    public ActionAnimation(Action action)
    {
        Action = _ => action();
    }

    public ActionAnimation(Action<CancellationToken> action)
    {
        Action = action;
    }

    public ActionAnimation(
        Action action,
        int delayMilliseconds,
        bool after = false) : this(action)
    {
        Delay = TimeSpan.FromMilliseconds(Math.Max(0, delayMilliseconds));
        After = after;
    }

    public ActionAnimation(
        Action action,
        bool after) : this(action)
    {
        After = after;
    }

    public Action<CancellationToken> Action { get; set; } = null!;
    public override int CurrentFrame { get; set; }
    public TimeSpan Delay { get; set; }

    public override Task<IAnimation> RunAsync(IAnimatable target)
    {
        var actualTarget = Target ?? target;
        var clone = Prepare();
        return clone.Status == AnimationStatus.Completed
            ? Task.FromResult<IAnimation>(clone)
            : RunDelayedAsync(clone, actualTarget);
    }

    private static async Task<IAnimation> RunDelayedAsync(
        ActionAnimation animation,
        IAnimatable target)
    {
        var delay = AnimationService.ScaleDuration(animation.Delay);
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay);

        if (animation.Status != AnimationStatus.Canceled)
            await AnimationService.PushAnimationAsync(animation, target);

        return animation;
    }

    public override IAnimation RunFireAndForget(IAnimatable target)
    {
        var actualTarget = Target ?? target;
        var clone = Prepare();
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

    private ActionAnimation Prepare()
    {
        ArgumentNullException.ThrowIfNull(Action);

        var clone = (ActionAnimation)MemberwiseClone();
        clone._cts = new CancellationTokenSource();
        clone._tcs = null;
        Interlocked.Exchange(ref clone._called, 0);
        clone.Status = AnimationStatus.Running;

        if (!AnimationService.IsInstant) return clone;

        void RunInstant()
        {
            clone.Action(clone._cts.Token);
            clone.Status = AnimationStatus.Completed;
            clone.RaiseCompleted();
        }

        if (AnimationService.UiAccessProvider.CheckAccess())
            RunInstant();
        else
            AnimationService.UiAccessProvider.Invoke(RunInstant);

        return clone;
    }

    public override void Cancel()
    {
        Status = AnimationStatus.Canceled;
        _cts?.Cancel();
        _tcs?.TrySetCanceled();
    }

    public override IAnimationFrame? ComputeNextFrame(IAnimatable target)
    {
        if (Status is AnimationStatus.Canceled or AnimationStatus.Completed) return null;

        if (Interlocked.CompareExchange(ref _called, 1, 0) == 0)
            return new ActionAnimationFrame(() =>
            {
                Action(_cts!.Token);
                Status = AnimationStatus.Completed;
                _tcs?.TrySetResult();
            });

        return null;
    }
}