using System;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL;

public static partial class Animations
{
    /// <summary>
    ///     缩小并淡出控件，动画结束后按 <paramref name="removeFromChildren" /> 决定从父容器移除还是隐藏。
    ///     控件已不可命中测试时直接返回。
    /// </summary>
    public static void AniDispose(
        Control control,
        bool removeFromChildren,
        ParameterizedThreadStart? callBack = null)
    {
        if (!control.IsHitTestVisible) return;
        control.IsHitTestVisible = false;

        var scale = control.RenderTransform as ScaleTransform ?? new ScaleTransform(1d, 1d);
        if (control.RenderTransform != scale)
        {
            control.RenderTransform = scale;
            control.RenderTransformOrigin = new Avalonia.RelativePoint(0.5d, 0.5d, Avalonia.RelativeUnit.Relative);
        }

        AniStart(
            new[]
            {
                AaScaleTransform(control, -0.08d, 200, ease: new AniEaseInFluent()),
                AaOpacity(control, -1d, 200, ease: new AniEaseOutFluent()),
                AaCode(() =>
                {
                    if (removeFromChildren)
                        (control.Parent as Panel)?.Children.Remove(control);
                    else
                        control.IsVisible = false;
                    callBack?.Invoke(control);
                }, after: true)
            },
            "AniDispose " + Guid.NewGuid().ToString("N"));
    }
}
