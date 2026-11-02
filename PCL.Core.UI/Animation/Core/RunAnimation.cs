using Avalonia;
using PCL.Core.UI.Animation.Animatable;

namespace PCL.Core.UI.Animation.Core;

/// <summary>
///     把动画与目标对象、目标属性绑定成可复用的对象，由调用方在需要时启动一次。
/// </summary>
/// <remarks>
///     继承 <see cref="AvaloniaObject" />，可在样式、资源或 XAML 中声明；
///     本身不监听任何事件，动画不会自动触发，必须显式调用 <see cref="Invoke" />。
///     目标对象与目标属性留空时，取动画自身通过附加属性设置的值。
/// </remarks>
public class RunAnimation : AvaloniaObject
{
    public static readonly StyledProperty<IAnimation?> AnimationProperty =
        AvaloniaProperty.Register<RunAnimation, IAnimation?>(nameof(Animation));

    public static readonly StyledProperty<AvaloniaProperty?> TargetPropertyProperty =
        AvaloniaProperty.Register<RunAnimation, AvaloniaProperty?>(nameof(TargetProperty));

    public IAnimation? Animation
    {
        get => GetValue(AnimationProperty);
        set => SetValue(AnimationProperty, value);
    }

    public AvaloniaProperty? TargetProperty
    {
        get => GetValue(TargetPropertyProperty);
        set => SetValue(TargetPropertyProperty, value);
    }

    /// <summary>
    ///     启动动画。
    /// </summary>
    /// <param name="associatedObject">未在动画上显式指定目标时使用的目标对象。</param>
    public void Invoke(AvaloniaObject? associatedObject = null)
    {
        var animation = Animation ?? throw new InvalidOperationException("未指定动画。");
        var animationObject = animation as AvaloniaObject;

        AvaloniaObject? targetObject;
        AvaloniaProperty? targetProperty;

        // 判断对象
        if (animationObject is not null
            && animationObject.IsSet(AnimationExtensions.TargetProperty))
            targetObject = animationObject.GetValue(AnimationExtensions.TargetProperty);
        else
            targetObject = associatedObject
                ?? throw new InvalidOperationException("未指定动画的目标对象。");

        // 判断属性
        if (animationObject is not null
            && animationObject.IsSet(AnimationExtensions.TargetPropertyProperty))
            targetProperty = animationObject.GetValue(AnimationExtensions.TargetPropertyProperty);
        else if (TargetProperty is not null)
            targetProperty = TargetProperty;
        else if (animation is AnimationGroup)
            // AnimationGroup 可以没有目标属性
            targetProperty = null;
        else
            throw new InvalidOperationException("未指定动画的目标属性。");

        animation.RunFireAndForget(new AvaloniaAnimatable(targetObject!, targetProperty));
    }
}