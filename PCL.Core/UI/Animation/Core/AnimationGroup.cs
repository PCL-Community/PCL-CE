using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using PCL.Core.UI.Animation.Animatable;
using PCL.Core.Utils;

namespace PCL.Core.UI.Animation.Core;

/// <summary>
/// 动画组的基类。
/// </summary>
[ContentProperty(nameof(Children))]
public abstract class AnimationGroup : AnimationBase
{
    public static readonly DependencyProperty ChildrenProperty =
        DependencyProperty.Register(
            nameof(Children),
            typeof(ObservableCollection<IAnimation>),
            typeof(AnimationGroup),
            new PropertyMetadata(null)); // 移除 OnChildrenChanged 回调，避免运行时冲突

    public ObservableCollection<IAnimation> Children
    {
        get => (ObservableCollection<IAnimation>)GetValue(ChildrenProperty);
        set => SetValue(ChildrenProperty, value);
    }

    /// <summary>
    /// 存储当前正在运行的动画实例。
    /// </summary>
    protected List<IAnimation> ChildrenCore { get; } = [];

    protected AnimationGroup()
    {
        // 确保集合初始化，但不进行自动同步
        SetCurrentValue(ChildrenProperty, new ObservableCollection<IAnimation>());
    }

    public override int CurrentFrame { get; set; }

    public override void Cancel()
    {
        Status = AnimationStatus.Canceled;

        CurrentFrame = 0;
        
        lock (ChildrenCore)
        {
            foreach (var child in ChildrenCore)
            {
                child.Cancel();
            }
            // 清理运行实例，断开引用
            ChildrenCore.Clear();
        }
    }
    
    public void CancelAndClear()
    {
        Cancel();
        // 如果需要清空定义的 Children 集合，应在 UI 线程操作
        AnimationService.UIAccessProvider.Invoke(() => Children.Clear());
    }

    public override IAnimationFrame? ComputeNextFrame(IAnimatable target)
    {
        return null;
    }
    
    protected static IAnimatable ResolveTarget(IAnimation animation, IAnimatable defaultTarget)
    {
        if (animation is not DependencyObject aniDependencyObject)
            return defaultTarget;

        DependencyObject? targetObject = null;
        DependencyProperty? targetProperty = null;

        // Target check
        if (WpfUtils.IsDependencyPropertySet(aniDependencyObject, AnimationExtensions.TargetProperty))
        {
            targetObject = (DependencyObject)aniDependencyObject.GetValue(AnimationExtensions.TargetProperty);
        }
        else if (defaultTarget is WpfAnimatable animatable)
        {
            targetObject = animatable.Owner;
        }

        // TargetProperty check
        if (WpfUtils.IsDependencyPropertySet(aniDependencyObject, AnimationExtensions.TargetPropertyProperty))
        {
            targetProperty = (DependencyProperty)aniDependencyObject.GetValue(AnimationExtensions.TargetPropertyProperty);
        }
        else if (defaultTarget is WpfAnimatable animatable)
        {
            targetProperty = animatable.Property;
        }

        // 如果都未解析出特定值，直接返回默认目标
        if (targetObject == null || targetProperty == null)
            return defaultTarget;

        return new WpfAnimatable(targetObject, targetProperty);
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