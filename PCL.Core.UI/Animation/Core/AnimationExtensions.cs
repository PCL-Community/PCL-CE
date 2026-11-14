using Avalonia;

namespace PCL.Core.UI.Animation.Core;

/// <summary>
///     动画的附加属性：把动画实例绑定到目标对象与目标属性。
/// </summary>
public static class AnimationExtensions
{
    #region 附加属性

    public static readonly AttachedProperty<AvaloniaObject?> TargetProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, AvaloniaObject?>(
            "Target",
            typeof(AnimationExtensions));

    public static void SetTarget(AvaloniaObject element, AvaloniaObject? value)
    {
        if (element is not IAnimation)
            throw new InvalidOperationException("AnimationExtensions.Target 只能附加到 IAnimation 实例上。");

        element.SetValue(TargetProperty, value);
    }

    public static AvaloniaObject? GetTarget(AvaloniaObject element)
    {
        return element.GetValue(TargetProperty);
    }

    public static readonly AttachedProperty<AvaloniaProperty?> TargetPropertyProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, AvaloniaProperty?>(
            "TargetProperty",
            typeof(AnimationExtensions));

    public static void SetTargetProperty(AvaloniaObject element, AvaloniaProperty? value)
    {
        if (element is not IAnimation)
            throw new InvalidOperationException(
                "AnimationExtensions.TargetProperty 只能附加到 IAnimation 实例上。");

        element.SetValue(TargetPropertyProperty, value);
    }

    public static AvaloniaProperty? GetTargetProperty(AvaloniaObject element)
    {
        return element.GetValue(TargetPropertyProperty);
    }

    #endregion
}