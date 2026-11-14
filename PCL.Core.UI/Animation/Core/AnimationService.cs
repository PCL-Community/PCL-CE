using System.Collections;
using System.Collections.Concurrent;
using System.Threading.Channels;
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
///     动画服务：负责帧调度、帧计算与 UI 线程赋值。
///     由生命周期服务自动启动，也可通过 <see cref="EnsureStarted" /> 惰性启动。
/// </summary>
[LifecycleService(LifecycleState.WindowCreating)]
public sealed class AnimationService : GeneralService
{
    private static Channel<(IAnimation Animation, IAnimatable Target)> _animationChannel = null!;
    private static Channel<(IAnimationFrame Frame, IAnimation Source)> _frameChannel = null!;

    private static readonly ConcurrentDictionary<string, IAnimation> NamedAnimations = new();
    private static IClock _clock = null!;

    private static TaskCompletionSource _nextTick = null!;

    private static int _taskCount;
    private static CancellationTokenSource _cts = null!;
    private static IUiAccessProvider? _uiAccessProvider;

    private static readonly Lock InitLock = new();
    private static bool _initialized;
    private static double? _speedOverride;
    private static int _suspensionCount;

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
        // 初始化 Channel 与 Dictionary
        _animationChannel = Channel.CreateUnbounded<(IAnimation, IAnimatable)>();
        _frameChannel = Channel.CreateUnbounded<(IAnimationFrame, IAnimation)>();

        // 根据核心数量来确定动画计算 Task 数量
        _taskCount = Environment.ProcessorCount;

        _cts = new CancellationTokenSource();
        _nextTick = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // 注册 ValueProcessor
        _RegisterValueProcessors();

        // 初始化 UI 线程访问提供器并启动赋值 Task
        var uiAccessProvider = UiAccessProvider;
        _ = uiAccessProvider.InvokeAsync(async () =>
        {
            while (await _frameChannel.Reader.WaitToReadAsync())
            {
                // 读取数据
                while (_frameChannel.Reader.TryRead(out var item))
                {
                    // 如果动画源已被标记取消，直接丢弃该帧，不进行处理
                    if (item.Source.Status == AnimationStatus.Canceled)
                        continue;

                    // 正常处理
                    item.Frame.GetAction()();
                }

                await Task.Yield();
            }
        });

        // 初始化 Clock 并注册 Tick 事件
        _clock = new AvaloniaRenderingClock(Fps);
        _clock.Tick += ClockOnTick;
        _clock.Start();

        // 运行动画计算 Task
        for (var i = 0; i < _taskCount; i++) _ = Task.Run(_AnimationComputeTaskAsync);

        TryInfo($"以最多 {_taskCount} 个线程初始化动画计算 Task");
    }

    private static void _Uninitialize()
    {
        // 取消动画计算 Task
        try
        {
            _cts.Cancel();
        }
        catch
        {
            // 忽略
        }

        _cts.Dispose();

        // 停止 Clock 并注销 Tick 事件
        if (_clock is not null)
        {
            _clock.Tick -= ClockOnTick;
            _clock.Stop();
        }

        try
        {
            _animationChannel?.Writer.TryComplete();
            _frameChannel?.Writer.TryComplete();
        }
        catch
        {
        }

        try
        {
            Volatile.Read(ref _nextTick)?.TrySetCanceled();
        }
        catch
        {
            // 忽略
        }

        CancelAll();
    }

    private static void ClockOnTick(object? sender, long e)
    {
        var next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var previous = Interlocked.Exchange(ref _nextTick, next);
        previous?.TrySetResult();
    }

    private static async Task _AnimationComputeTaskAsync()
    {
        // 本地动画列表，确保没有一直无法计算的动画
        var animationList = new List<(IAnimation Animation, IAnimatable Target)>(8);

        // 持续监听 Channel 中的动画
        while (!_cts.IsCancellationRequested)
        {
            // 读取所有可用的动画到本地列表
            while (_animationChannel.Reader.TryRead(out var animation))
                animationList.Add(animation);

            // 如果没有动画，直接等下一帧
            if (animationList.Count == 0)
            {
                if (!await _WaitNextFrameAsync()) return;
                continue;
            }

            for (var i = animationList.Count - 1; i >= 0; i--)
            {
                var animationEntry = animationList[i];

                if (animationEntry.Animation.Status is AnimationStatus.Canceled
                    or AnimationStatus.Completed)
                {
                    try
                    {
                        await UiAccessProvider.InvokeAsync(animationEntry.Animation.RaiseCompleted);
                    }
                    catch (Exception ex)
                    {
                        LogWrapper.Warn(
                            "Animation",
                            $"触发动画完成事件失败：{animationEntry.Animation.Name}，{ex.Message}");
                    }

                    if (!string.IsNullOrEmpty(animationEntry.Animation.Name))
                        ((ICollection<KeyValuePair<string, IAnimation>>)NamedAnimations)
                            .Remove(
                                new KeyValuePair<string, IAnimation>(
                                    animationEntry.Animation.Name,
                                    animationEntry.Animation));

                    animationList.RemoveAt(i);
                    continue;
                }

                // 计算动画的下一帧
                var frame = animationEntry.Animation.ComputeNextFrame(animationEntry.Target);
                // 如果没有计算帧（动画组），跳过
                if (frame is null) continue;
                // 将动画帧写入 Channel
                _frameChannel.Writer.TryWrite((frame, animationEntry.Animation));
                // 增加当前帧计数
                animationEntry.Animation.CurrentFrame++;
            }

            // 等待 Tick 事件的通知
            if (!await _WaitNextFrameAsync()) return;
        }
    }

    private static async Task<bool> _WaitNextFrameAsync()
    {
        try
        {
            await Volatile.Read(ref _nextTick).Task;
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
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

        animation.Completed += (_, _) => tcs.SetResult();

        _animationChannel.Writer.TryWrite((animation, target));
        return tcs.Task;
    }

    internal static void PushAnimationFireAndForget(IAnimation animation, IAnimatable target)
    {
        EnsureStarted();
        _HandleNamedAnimationConflict(animation);

        _animationChannel.Writer.TryWrite((animation, target));
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