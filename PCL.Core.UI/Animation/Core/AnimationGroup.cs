using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Metadata;
using PCL.Core.UI.Animation.Animatable;

namespace PCL.Core.UI.Animation.Core;

/// <summary>
///     动画组的基类。
/// </summary>
public abstract class AnimationGroup : AnimationBase
{
    public static readonly StyledProperty<ObservableCollection<IAnimation>?> ChildrenProperty =
        AvaloniaProperty.Register<AnimationGroup, ObservableCollection<IAnimation>?>(
            nameof(Children));

    protected AnimationGroup()
    {
        SetCurrentValue(ChildrenProperty, []);
    }

    [Content]
    public ObservableCollection<IAnimation> Children
    {
        get
        {
            if (GetValue(ChildrenProperty) is { } children)
                return children;

            children = [];
            SetCurrentValue(ChildrenProperty, children);
            return children;
        }
        set => SetValue(ChildrenProperty, value ?? []);
    }

    /// <summary>
    ///     存储当前正在运行的动画实例。
    /// </summary>
    protected List<IAnimation> ChildrenCore { get; } = [];

    public override int CurrentFrame { get; set; }

    public override void Cancel()
    {
        Status = AnimationStatus.Canceled;

        CurrentFrame = 0;

        lock (ChildrenCore)
        {
            foreach (var child in ChildrenCore) child.Cancel();
            // 清理运行实例，断开引用
            ChildrenCore.Clear();
        }
    }

    public void CancelAndClear()
    {
        Cancel();
        // 如果需要清空定义的 Children 集合，应在 UI 线程操作
        AnimationService.UiAccessProvider.Invoke(() => Children.Clear());
    }

    public override IAnimationFrame? ComputeNextFrame(IAnimatable target)
    {
        return null;
    }

    protected static IAnimatable ResolveTarget(IAnimation animation, IAnimatable defaultTarget)
    {
        if (animation is not AvaloniaObject animationObject)
            return defaultTarget;

        if (animationObject is AnimationBase { Target: { } explicitTarget })
            return explicitTarget;

        AvaloniaObject? targetObject = null;
        AvaloniaProperty? targetProperty = null;

        // Target check
        if (animationObject.IsSet(AnimationExtensions.TargetProperty))
            targetObject = animationObject.GetValue(AnimationExtensions.TargetProperty);
        else if (defaultTarget is AvaloniaAnimatable animatable)
            targetObject = animatable.Owner;

        // TargetProperty check
        if (animationObject.IsSet(AnimationExtensions.TargetPropertyProperty))
            targetProperty = animationObject.GetValue(AnimationExtensions.TargetPropertyProperty);
        else if (defaultTarget is AvaloniaAnimatable animatable)
            targetProperty = animatable.Property;

        // 如果都未解析出特定值，直接返回默认目标
        if (targetObject is null || targetProperty is null)
            return defaultTarget;

        return new AvaloniaAnimatable(targetObject, targetProperty);
    }

    protected static Task CreateChildAwaiter(IAnimation animation)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (animation.Status == AnimationStatus.Completed)
        {
            tcs.TrySetResult();
            return tcs.Task;
        }

        EventHandler? handler = null;
        handler = (_, _) =>
        {
            animation.Completed -= handler;
            tcs.TrySetResult();
        };

        animation.Completed += handler;

        if (animation.Status != AnimationStatus.Completed) return tcs.Task;

        animation.Completed -= handler;
        tcs.TrySetResult();

        return tcs.Task;
    }
}