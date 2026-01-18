using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Microsoft.Xaml.Behaviors;
using PCL.Core.UI.Animation.Animatable;
using PCL.Core.Utils;

namespace PCL.Core.UI.Animation.Core;

[ContentProperty(nameof(Animation))]
public class RunAnimation : TriggerAction<DependencyObject>
{
    public static readonly DependencyProperty AnimationProperty = DependencyProperty.Register(
        nameof(Animation),
        typeof(IAnimation),
        typeof(RunAnimation),
        new PropertyMetadata(default(IAnimation)));

    public IAnimation Animation
    {
        get => (IAnimation)GetValue(AnimationProperty);
        set => SetValue(AnimationProperty, value);
    }

    public static readonly DependencyProperty TargetPropertyProperty = DependencyProperty.Register(
        nameof(TargetProperty),
        typeof(DependencyProperty),
        typeof(RunAnimation),
        new PropertyMetadata(default(DependencyProperty)));

    public DependencyProperty TargetProperty
    {
        get => (DependencyProperty)GetValue(TargetPropertyProperty);
        set => SetValue(TargetPropertyProperty, value);
    }

    protected override void Invoke(object parameter)
    {
        DependencyObject? targetObject;
        DependencyProperty? targetProperty;

        var aniDependencyObject = (DependencyObject)Animation;

        // 判断对象
        if (WpfUtils.IsDependencyPropertySet(aniDependencyObject, AnimationExtensions.TargetProperty))
        {
            targetObject = (DependencyObject)aniDependencyObject.GetValue(AnimationExtensions.TargetProperty);
        }
        else
        {
            if (AssociatedObject is not null)
            {
                targetObject = AssociatedObject;
            }
            else
            {
                // 按理来说不可能出现这种情况，但是还是抛个异常吧
                throw new InvalidOperationException("未指定动画的目标对象。");
            }
        }

        // 判断属性
        if (WpfUtils.IsDependencyPropertySet(aniDependencyObject, AnimationExtensions.TargetPropertyProperty))
        {
            targetProperty =
                (DependencyProperty)aniDependencyObject.GetValue(AnimationExtensions.TargetPropertyProperty);
        }
        else
        {
            if (WpfUtils.IsDependencyPropertySet(this, TargetPropertyProperty))
            {
                targetProperty = TargetProperty;
            }
            else
            {
                if (Animation is not AnimationGroup)
                {
                    // 这里就有可能出现这种情况
                    throw new InvalidOperationException("未指定动画的目标属性。");
                }

                // AnimationGroup 可以没有目标属性
                targetProperty = null;
            }
        }
        
        Animation.RunFireAndForget(new WpfAnimatable(targetObject, targetProperty));
    }
}