using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.UI.Animation.Animatable;
using PCL.Core.UI.Animation.Clock;
using PCL.Core.UI.Animation.UIAccessProvider;
using PCL.Core.UI.Animation.ValueProcessor;
using PCL.Core.Utils.Threading;

namespace PCL.Core.UI.Animation.Core;

[LifecycleService(LifecycleState.WindowCreating)]
public sealed class AnimationService : GeneralService
{
    #region Lifecycle

    private static LifecycleContext? _context;
    private static LifecycleContext Context => _context!;

    private AnimationService() : base("animation", "动画")
    {
        _context = ServiceContext;
    }
    
    public override void Start()
    {
        _Initialize();
    }

    public override void Stop()
    {
        _Uninitialize();
    }

    #endregion

    private static void _RegisterValueProcessors()
    {
        // 在这里注册所有的 ValueProcessor
        ValueProcessorManager.Register(new DoubleValueProcessor());
        ValueProcessorManager.Register(new MatrixValueProcessor());
        ValueProcessorManager.Register(new NColorValueProcessor());
        ValueProcessorManager.Register(new NRotateTransformValueProcessor());
        ValueProcessorManager.Register(new NScaleTransformValueProcessor());
        ValueProcessorManager.Register(new PointValueProcessor());
        ValueProcessorManager.Register(new ThicknessValueProcessor());
    }

    private static Channel<(IAnimation Animation, IAnimatable Target)> _animationChannel = null!;
    // private static Channel<IAnimationFrame> _frameChannel = null!;
    private static Channel<(IAnimationFrame Frame, IAnimation Source)> _frameChannel = null!;
    // private static ConcurrentDictionary<IAnimatable, IAnimationFrame> _frameDictionary = null!;
    private static ConcurrentDictionary<string, IAnimation> _namedAnimations = new();
    private static IClock _clock = null!;
    private static AsyncCountResetEvent _resetEvent = null!;
    private static int _taskCount;
    private static CancellationTokenSource _cts = null!;
    
    public static int Fps { get; set; } = 60;
    public static double Scale { get; set; } = 0.1d;

    public static IUIAccessProvider UIAccessProvider { get; private set; } = null!;
    
    private static void _Initialize()
    {
        // 初始化 Channel 与 Dictionary
        _animationChannel = Channel.CreateUnbounded<(IAnimation, IAnimatable)>();
        // _frameChannel = Channel.CreateUnbounded<IAnimationFrame>();
        _frameChannel = Channel.CreateUnbounded<(IAnimationFrame, IAnimation)>();
        
        // 根据核心数量来确定动画计算 Task 数量
        _taskCount = Environment.ProcessorCount;
        Context.Info($"以最多 {_taskCount} 个线程初始化动画计算 Task");

        // 初始化 CancellationTokenSource 与 ResetEvent
        _cts = new CancellationTokenSource();
        _resetEvent = new AsyncCountResetEvent();
        
        // 注册 ValueProcessor
        _RegisterValueProcessors();
        
        // 初始化 UI 线程访问提供器并启动赋值 Task
        UIAccessProvider = new WpfUIAccessProvider(Lifecycle.CurrentApplication.Dispatcher);
        _ = UIAccessProvider.InvokeAsync(async () =>
        {
            if (_cts.IsCancellationRequested) return;
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
        _clock = new WinMMClock(Fps);
        _clock.Tick += ClockOnTick;
        _clock.Start();
        
        // 运行动画计算 Task
        for (var i = 0; i < _taskCount; i++)
        {
            _ = Task.Run(_AnimationComputeTaskAsync);
        }
    }

    private static void _Uninitialize()
    {
        // 取消动画计算 Task
        _cts.Cancel();
        _cts.Dispose();
        
        // 停止 Clock 并注销 Tick 事件
        _clock.Tick -= ClockOnTick;
        _clock.Stop();
        
        // 将 ResetEvent 释放
        _resetEvent.Dispose();
        
        // 清理 Dictionary
        _namedAnimations.Clear();
    }

    private static void ClockOnTick(object? sender, long e)
    {
        // 通知所有等待的动画计算 Task 进行下一帧计算
        _resetEvent.Set(_taskCount);
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
            {
                // 将动画添加到本地列表
                animationList.Add(animation);
            }

            // 如果没有动画，直接等下一帧
            if (animationList.Count == 0)
            {
                await _resetEvent.WaitAsync();
                continue;
            }

            for (var i = animationList.Count - 1; i >= 0; i--)
            {
                // TODO: 支持缓存动画计算结果 (由 AnimationData 支持)
                
                // 从列表中获取动画
                var animationEntry = animationList[i];
                            
                // 如果动画已经完成或被取消，则从列表中移除
                if (animationEntry.Animation.Status is AnimationStatus.Canceled or AnimationStatus.Completed)
                {
                    animationEntry.Animation.RaiseCompleted();
                    
                    if (!string.IsNullOrEmpty(animationEntry.Animation.Name))
                    {
                        // 使用显式接口
                        ((ICollection<KeyValuePair<string, IAnimation>>)_namedAnimations)
                            .Remove(new KeyValuePair<string, IAnimation>(animationEntry.Animation.Name, animationEntry.Animation));
                    }
                    
                    animationList.RemoveAt(i);
                    continue;
                }
                
                // 计算动画的下一帧
                var frame = animationEntry.Animation.ComputeNextFrame(animationEntry.Target);
                // 如果没有计算帧（当动画为 SequentialAnimationGroup 或 ParallelAnimationGroup 这种动画集合时），跳过
                if (frame is null) continue;
                // 将动画帧写入 Channel
                _frameChannel.Writer.TryWrite((frame, animationEntry.Animation));
                // 增加当前帧计数
                animationEntry.Animation.CurrentFrame++;
            }
            
            // 等待 Tick 事件的通知
            await _resetEvent.WaitAsync();
        }
    }

    private static void HandleNamedAnimationConflict(IAnimation animation)
    {
        if (string.IsNullOrEmpty(animation.Name)) return;

        _namedAnimations.AddOrUpdate(
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
        HandleNamedAnimationConflict(animation);
        
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        animation.Completed += (_, _) => tcs.SetResult();
        
        _animationChannel.Writer.TryWrite((animation, target));
        return tcs.Task;
    }
    
    internal static void PushAnimationFireAndForget(IAnimation animation, IAnimatable target)
    {
        HandleNamedAnimationConflict(animation);
        
        _animationChannel.Writer.TryWrite((animation, target));
    }
    
    public static void CancelAnimationByName(string name)
    {
        if (_namedAnimations.TryRemove(name, out var animation))
        {
            animation.Cancel();
        }
    }
}