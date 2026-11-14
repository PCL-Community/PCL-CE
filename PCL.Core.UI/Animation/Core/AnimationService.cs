using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.IoC;
using PCL.Core.Logging;
using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.Clock;
using PCL.Core.UI.Animation.Easings;
using PCL.Core.UI.Animation.UIAccessProvider;
using PCL.Core.UI.Animation.ValueProcessor;

namespace PCL.Core.UI.Animation.Core;

/// <summary>
///     动画服务：在 UI 线程的渲染帧回调里同步计算并写回属性。
///     没有活动动画时不订阅帧源，避免把合成节拍锁在 60Hz 定时器上。
/// </summary>
[LifecycleService(LifecycleState.WindowCreating)]
public sealed class AnimationService : GeneralService
{
    private static readonly List<(IAnimation Animation, IAnimatable Target)> ActiveAnimations = new(8);
    private static readonly ConcurrentDictionary<string, IAnimation> NamedAnimations = new();
    private static AvaloniaRenderingClock? _clock;
    private static int _inTick;
    private static IUiAccessProvider? _uiAccessProvider;

    private static readonly Lock InitLock = new();
    private static bool _initialized;
    private static double? _speedOverride;
    private static int _suspensionCount;

    /// <summary>手动逐帧驱动使用的采样率。运行中的 UI 动画按显示器刷新率采样，不受此值限速。</summary>
    public static int Fps { get; set; } = 60;

    public static double Speed
    {
        get =>
            _speedOverride
            ?? (ConfigService.IsInitialized ? MapSpeedValue(Config.Debug.AnimationSpeed) : 1d);
        set => _speedOverride = value;
    }

    public static bool IsEnabled => Speed > 0d;

    public static bool IsInstant => Speed is <= 0d or >= 200d;

    public static bool IsSuspended => Volatile.Read(ref _suspensionCount) != 0;

    public static IUiAccessProvider UiAccessProvider =>
        _uiAccessProvider ??= new AvaloniaUiAccessProvider();

    public static double MapSpeedValue(int value)
    {
        if (value <= 0) return 0d;

        return value >= 30
            ? 200d
            : Math.Clamp(value * 0.1d + 0.1d, 0.1d, 3d);
    }

    public static void ResetSpeed()
    {
        _speedOverride = null;
    }

    public static TimeSpan ScaleDuration(TimeSpan duration)
    {
        var speed = Speed;
        if (speed is <= 0d or >= 200d) return TimeSpan.Zero;

        return TimeSpan.FromTicks(
            Math.Max(0L, (long)Math.Round(duration.Ticks / speed)));
    }

    public static void BeginControl()
    {
        Interlocked.Increment(ref _suspensionCount);
    }

    public static void EndControl()
    {
        Interlocked.Decrement(ref _suspensionCount);
    }

    public static IDisposable SuspendControl()
    {
        BeginControl();
        return new ControlSuspension();
    }

    public static bool IsAnimationRunning(string name)
    {
        return !string.IsNullOrEmpty(name) && NamedAnimations.ContainsKey(name);
    }

    public static void CancelAll()
    {
        foreach (var pair in NamedAnimations)
            pair.Value.Cancel();
        NamedAnimations.Clear();
    }

    public static void Run(
        IAnimation animation,
        string name = "",
        bool refreshTime = false)
    {
        Run(
            new List<IAnimation>
            {
                animation
            },
            name,
            refreshTime);
    }

    public static void Run(
        IList animations,
        string name = "",
        bool refreshTime = false)
    {
        var list = new List<IAnimation>();
        FlattenAnimations(animations, list);
        if (list.Count == 0) return;

        var root = new SequentialAnimationGroup
        {
            Name = name
        };
        var parallel = new ParallelAnimationGroup();

        foreach (var animation in list)
        {
            if (animation is AnimationBase { After: true } && parallel.Children.Count > 0)
            {
                root.Children.Add(parallel);
                parallel = new ParallelAnimationGroup();
            }

            parallel.Children.Add(animation);
        }

        if (parallel.Children.Count > 0)
            root.Children.Add(parallel);
        if (root.Children.Count == 0) return;

        _ = refreshTime;
        root.RunFireAndForget(EmptyAnimatable.Instance);
    }

    public static Task FadeAsync(
        Visual target,
        double from,
        double to,
        double baseMilliseconds)
    {
        var animation = new DoubleFromToAnimation
        {
            Target = new AvaloniaAnimatable(target, Visual.OpacityProperty),
            From = from,
            HasFrom = true,
            To = to,
            Duration = TimeSpan.FromMilliseconds(Math.Max(0d, baseMilliseconds)),
            Easing = new FluentEaseOut()
        };

        return animation.RunAsync(EmptyAnimatable.Instance);
    }

    public static void DisposeControl(
        Control control,
        bool removeFromChildren,
        Action<Control>? callBack = null,
        double scale = 0.92d,
        bool collapseHeight = false,
        string? animationName = null)
    {
        if (!control.IsHitTestVisible)
        {
            Finish();
            return;
        }

        control.IsHitTestVisible = false;
        if (!IsEnabled
            || IsSuspended
            || !control.IsLoaded
            || TopLevel.GetTopLevel(control) is null)
        {
            Finish();
            return;
        }

        var animations = new List<IAnimation>
        {
            new ScaleTransformAnimation(control, scale - 1d, 200, ease: new FluentEaseIn()),
            new OpacityAnimation(control, -control.Opacity, 200, ease: new FluentEaseOut())
        };
        if (collapseHeight)
            animations.Add(
                new HeightAnimation(
                    control,
                    -control.Bounds.Height,
                    150,
                    100,
                    new FluentEaseOut()));
        animations.Add(new ActionAnimation(Finish, 0, true));

        Run(animations, animationName ?? $"Dispose {Guid.NewGuid()}");
        return;

        void Finish()
        {
            if (removeFromChildren)
            {
                if (!TryRemoveFromParent(control))
                    control.IsVisible = false;
            }
            else
            {
                control.IsVisible = false;
            }

            callBack?.Invoke(control);
        }
    }

    public static bool TryRemoveFromParent(Control control)
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                return true;
            case Decorator decorator when ReferenceEquals(decorator.Child, control):
                decorator.Child = null;
                return true;
            case ContentControl contentControl
                when ReferenceEquals(contentControl.Content, control):
                contentControl.Content = null;
                return true;
            default:
                return false;
        }
    }

    private static void FlattenAnimations(IList source, ICollection<IAnimation> target)
    {
        foreach (var item in source)
            switch (item)
            {
                case IAnimation animation:
                    target.Add(animation);
                    break;
                case IList list:
                    FlattenAnimations(list, target);
                    break;
            }
    }

    internal static void RegisterValueProcessorsForTest()
    {
        _RegisterValueProcessors();
    }

    private static void _RegisterValueProcessors()
    {
        ValueProcessorManager.Register(new DoubleValueProcessor());
        ValueProcessorManager.Register(new MatrixValueProcessor());
        ValueProcessorManager.Register(new NColorValueProcessor());
        ValueProcessorManager.Register(new NRotateTransformValueProcessor());
        ValueProcessorManager.Register(new NScaleTransformValueProcessor());
        ValueProcessorManager.Register(new PointValueProcessor());
        ValueProcessorManager.Register(new ThicknessValueProcessor());
    }

    private static void _Initialize()
    {
        _RegisterValueProcessors();
        TryInfo("动画服务已就绪，帧更新在 UI 线程同步应用");
    }

    private static void _Uninitialize()
    {
        StopClock();
        _clock = null;
        ActiveAnimations.Clear();
        CancelAll();
    }

    private static void EnsureClock()
    {
        _clock ??= new AvaloniaRenderingClock(int.MaxValue);
        if (_clock.IsRunning) return;

        _clock.Tick += ClockOnTick;
        _clock.Start();
    }

    private static void StopClock()
    {
        if (_clock is not { IsRunning: true }) return;

        _clock.Tick -= ClockOnTick;
        _clock.Stop();
    }

    private static void ClockOnTick(object? sender, long frame)
    {
        _ = frame;
        if (Interlocked.Exchange(ref _inTick, 1) == 1) return;

        try
        {
            PumpActiveAnimations();
        }
        finally
        {
            Interlocked.Exchange(ref _inTick, 0);
        }
    }

    private static void PumpActiveAnimations()
    {
        var count = ActiveAnimations.Count;
        if (count == 0)
        {
            StopClock();
            return;
        }

        var now = Stopwatch.GetTimestamp();
        for (var i = count - 1; i >= 0; i--)
        {
            if (i >= ActiveAnimations.Count) continue;

            var (animation, target) = ActiveAnimations[i];
            if (IsTerminal(animation))
            {
                CompleteAndRemove(i, animation);
                continue;
            }

            try
            {
                if (animation is IClockSampledAnimation sampled)
                    sampled.ArmClock(now);

                animation.ComputeNextFrame(target)?.Apply();
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(
                    "Animation",
                    $"动画帧应用失败：{animation.Name}，{ex.Message}");
                animation.Cancel();
            }

            if (IsTerminal(animation)
                || animation is IClockSampledAnimation { IsClockFinished: true })
                CompleteAndRemove(i, animation);
        }

        if (ActiveAnimations.Count == 0)
            StopClock();
    }

    private static bool IsTerminal(IAnimation animation)
    {
        return animation.Status is AnimationStatus.Canceled or AnimationStatus.Completed;
    }

    private static void CompleteAndRemove(int index, IAnimation animation)
    {
        if (animation is AnimationBase { Status: AnimationStatus.Running } animationBase)
            animationBase.Status = AnimationStatus.Completed;

        ActiveAnimations.RemoveAt(index);
        try
        {
            animation.RaiseCompleted();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(
                "Animation",
                $"触发动画完成事件失败：{animation.Name}，{ex.Message}");
        }

        if (string.IsNullOrEmpty(animation.Name)) return;

        ((ICollection<KeyValuePair<string, IAnimation>>)NamedAnimations)
            .Remove(
                new KeyValuePair<string, IAnimation>(animation.Name, animation));
    }

    private static void _HandleNamedAnimationConflict(IAnimation animation)
    {
        if (string.IsNullOrEmpty(animation.Name)) return;

        NamedAnimations.AddOrUpdate(
            animation.Name,
            animation, // 如果不存在，直接添加
            (_, existingAnimation) =>
            {
                // 如果已存在同名动画，取消旧动画
                existingAnimation.Cancel();
                // 替换为新动画
                return animation;
            });
    }

    internal static Task PushAnimationAsync(IAnimation animation, IAnimatable target)
    {
        EnsureStarted();
        _HandleNamedAnimationConflict(animation);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        animation.Completed += (_, _) => tcs.TrySetResult();
        Enqueue(animation, target);
        return tcs.Task;
    }

    internal static void PushAnimationFireAndForget(IAnimation animation, IAnimatable target)
    {
        EnsureStarted();
        _HandleNamedAnimationConflict(animation);
        Enqueue(animation, target);
    }

    private static void Enqueue(IAnimation animation, IAnimatable target)
    {
        if (UiAccessProvider.CheckAccess())
            Activate(animation, target);
        else
            _ = UiAccessProvider.InvokeAsync(() => Activate(animation, target));
    }

    private static void Activate(IAnimation animation, IAnimatable target)
    {
        if (!_initialized) return;

        if (IsTerminal(animation))
        {
            if (animation is AnimationBase { Status: AnimationStatus.Running } animationBase)
                animationBase.Status = AnimationStatus.Completed;
            try
            {
                animation.RaiseCompleted();
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(
                    "Animation",
                    $"触发动画完成事件失败：{animation.Name}，{ex.Message}");
            }

            return;
        }

        ActiveAnimations.Add((animation, target));
        EnsureClock();
    }

    public static void CancelAnimationByName(string name)
    {
        if (NamedAnimations.TryRemove(name, out var animation)) animation.Cancel();
    }

    /// <summary>
    ///     惰性启动动画服务；重复调用无副作用。
    /// </summary>
    public static void EnsureStarted()
    {
        if (_initialized) return;

        lock (InitLock)
        {
            if (_initialized) return;
            _Initialize();
            _initialized = true;
        }
    }

    /// <summary>
    ///     停止动画服务并释放帧循环资源。
    /// </summary>
    public static void StopService()
    {
        lock (InitLock)
        {
            if (!_initialized) return;
            _initialized = false;
            _Uninitialize();
        }
    }

    private sealed class ControlSuspension : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            EndControl();
        }
    }

    #region Lifecycle

    private static LifecycleContext? _context;
    private static LifecycleContext Context => _context!;

    private AnimationService() : base("animation", "动画")
    {
        _context = ServiceContext;
    }

    public override void Start()
    {
        EnsureStarted();
    }

    public override void Stop()
    {
        StopService();
    }

    private static void TryInfo(string message)
    {
        try
        {
            _context?.Info(message);
        }
        catch
        {
            LogWrapper.Debug("Animation", message);
        }
    }

    #endregion
}