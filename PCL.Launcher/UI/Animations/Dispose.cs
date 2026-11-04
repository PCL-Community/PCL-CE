using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL;

public static partial class Animations
{
    /// <summary>
    ///     收起控件并移除或隐藏：缩放、淡出，可选高度收拢，动画结束后在 UI 线程执行回调。
    ///     这是动画模块唯一的收起实现，控件自身的 <c>AniDispose</c> 都转调本方法。
    ///     控件已不可交互、未加载或动画关闭时立即收尾。
    /// </summary>
    /// <param name="control">要收起的控件。</param>
    /// <param name="removeFromChildren">为 <see langword="true" /> 时从父容器移除，否则仅隐藏。</param>
    /// <param name="callBack">收尾后的回调，参数为本控件。</param>
    /// <param name="scale">缩放目标比例，小于 1 表示缩小。</param>
    /// <param name="collapseHeight">是否同时把高度收拢到 0。</param>
    /// <param name="animationName">动画组名；控件在卸载时可按同名停止。</param>
    public static void AniDispose(
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

        if (!_CanDisposeAnimate(control))
        {
            Finish();
            return;
        }

        var transform = control.RenderTransform as ScaleTransform;
        if (transform is null)
        {
            control.RenderTransformOrigin = RelativePoint.Center;
            transform = new ScaleTransform(1d, 1d);
            control.RenderTransform = transform;
        }

        var animations = new List<AniData>
        {
            AaScaleTransform(control, scale - transform.ScaleX, 200, ease: new AniEaseInFluent()),
            AaOpacity(control, -control.Opacity, 200, ease: new AniEaseOutFluent())
        };
        if (collapseHeight)
            animations.Add(
                AaHeight(control, -control.Bounds.Height, 150, 100, new AniEaseOutFluent()));
        animations.Add(AaCode(Finish, after: true));

        AniStart(animations, animationName ?? "AniDispose " + GetUuid());
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

    private static bool _CanDisposeAnimate(Control control)
    {
        return AniSpeed > 0d
            && control.IsLoaded
            && TopLevel.GetTopLevel(control) is not null
            && !AniExitsAnyControl();
    }

    internal static bool TryRemoveFromParent(Control control)
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
}