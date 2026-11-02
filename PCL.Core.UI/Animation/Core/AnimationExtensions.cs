using Avalonia;
using Avalonia.Media;
using PCL.Core.UI.Animation.Easings;

namespace PCL.Core.UI.Animation.Core;

public static class AnimationExtensions
{
    public static void Animate(
        this AvaloniaObject target,
        TimeSpan? duration = null,
        TimeSpan? delay = null,
        IEasing? easing = null,
        AnimationValueType valueType = AnimationValueType.Relative,
        int iterationCount = 1,
        double? width = null,
        double? height = null,
        double? opacity = null,
        double? radius = null,
        TranslateTransform? translate = null,
        double? translateX = null,
        double? translateY = null,
        RotateTransform? rotate = null,
        double? rotateAngle = null,
        ScaleTransform? scale = null,
        double? scaleX = null,
        double? scaleY = null,
        SkewTransform? skew = null,
        double? skewX = null,
        double? skewY = null,
        Thickness? margin = null,
        double? marginLeft = null,
        double? marginTop = null,
        double? marginRight = null,
        double? marginBottom = null,
        Thickness? padding = null,
        double? paddingLeft = null,
        double? paddingTop = null,
        double? paddingRight = null,
        double? paddingBottom = null,
        NColor? background = null,
        NColor? foreground = null)
    {
        // TODO(ENG-04): 实现快速调用动画入口。
    }

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