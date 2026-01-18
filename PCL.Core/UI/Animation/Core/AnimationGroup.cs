using System.Collections.ObjectModel;
using System.Linq;
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
    public static readonly DependencyProperty ChildrenProperty = DependencyProperty.Register(
        nameof(Children), typeof(ObservableCollection<IAnimation>), typeof(SequentialAnimationGroup));

    public ObservableCollection<IAnimation> Children
    {
        get => (ObservableCollection<IAnimation>)GetValue(ChildrenProperty);
        set => SetValue(ChildrenProperty, value);
    }
    
    protected AnimationGroup()
    {
        SetCurrentValue(ChildrenProperty, new ObservableCollection<IAnimation>());
    }
    
    public override bool IsCompleted => Children.All(child => child.IsCompleted);
    public override int CurrentFrame { get; set; }

    public override void Cancel()
    {
        // 重置值
        CurrentFrame = 0;
        
        // 取消所有子动画
        foreach (var child in Children)
        {
            child.Cancel();
        }
    }

    public override IAnimationFrame? ComputeNextFrame(IAnimatable target)
    {
        return null;
    }
    
    protected static IAnimatable ResolveTarget(IAnimation animation, IAnimatable defaultTarget)
    {
        if (animation is not DependencyObject aniDependencyObject)
            return defaultTarget;

        DependencyObject? targetObject;
        DependencyProperty? targetProperty;

        // Target
        if (WpfUtils.IsDependencyPropertySet(aniDependencyObject, AnimationExtensions.TargetProperty))
        {
            targetObject = (DependencyObject)aniDependencyObject.GetValue(AnimationExtensions.TargetProperty);
        }
        else
        {
            targetObject = ((WpfAnimatable)defaultTarget).Owner; // 默认用父级
        }

        // TargetProperty
        if (WpfUtils.IsDependencyPropertySet(aniDependencyObject, AnimationExtensions.TargetPropertyProperty))
        {
            targetProperty = (DependencyProperty)aniDependencyObject.GetValue(AnimationExtensions.TargetPropertyProperty);
        }
        else
        {
            targetProperty = ((WpfAnimatable)defaultTarget).Property; // 默认用父级
        }

        return new WpfAnimatable(targetObject, targetProperty);
    }
}