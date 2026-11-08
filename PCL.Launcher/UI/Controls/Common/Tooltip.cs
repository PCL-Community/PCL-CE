using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.Logging;

namespace PCL.UI.Controls;

/// <summary>
///     全局提示服务：接管控件的 <see cref="ToolTip.TipProperty" />，用自绘的浮层替代原生提示，
///     提供淡入缩放动画、光标跟随、滚轮/点击关闭与统一配色。
///     未调用 <see cref="Enable" /> 时完全不影响原生 ToolTip。
/// </summary>
public sealed class Tooltip
{
    private const string Module = "UI.Tooltip";

    private const double ScaleClosed = 0.97d;
    private const double ShadowBlur = 18d;
    private const int MaxContentWidth = 676;
    private const double TipFontSize = 12.5d;
    private const double TipLineHeight = 17d;
    private const int AnimLength = 80;
    private const int AnimExit = 80;
    private const double CursorGap = 15d;

    private static readonly Thickness InnerPad = new(12d, 10d, 12d, 10d);

    private static bool _enabled;
    private static IDisposable? _openingSubscription;
    private static IDisposable? _pressSubscription;
    private static IDisposable? _releaseSubscription;
    private static bool _pointerPressed;

    private static Control? _target;
    private static Popup? _flyout;
    private static Border? _shell;
    private static ScaleTransform? _scaler;
    private static bool _closing;

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Tooltip, Control, bool>("IsEnabled", true);

    public static readonly AttachedProperty<bool> FollowCursorProperty =
        AvaloniaProperty.RegisterAttached<Tooltip, Control, bool>("FollowCursor", true);

    private Tooltip()
    {
    }

    public static void SetIsEnabled(Control element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    public static bool GetIsEnabled(Control element)
    {
        return element.GetValue(IsEnabledProperty);
    }

    public static void SetFollowCursor(Control element, bool value)
    {
        element.SetValue(FollowCursorProperty, value);
    }

    public static bool GetFollowCursor(Control element)
    {
        return element.GetValue(FollowCursorProperty);
    }

    public static void Enable()
    {
        if (_enabled) return;

        _enabled = true;
        _openingSubscription = ToolTip.ToolTipOpeningEvent.AddClassHandler<Control>(
            _OnToolTipOpening,
            RoutingStrategies.Direct | RoutingStrategies.Bubble,
            true);
        _pressSubscription = InputElement.PointerPressedEvent.AddClassHandler<InputElement>(
            (_, _) =>
            {
                _pointerPressed = true;
                Dismiss();
            },
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true);
        _releaseSubscription = InputElement.PointerReleasedEvent.AddClassHandler<InputElement>(
            (_, _) => _pointerPressed = false,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            true);

        if (Application.Current is { } application)
            application.ActualThemeVariantChanged += _OnThemeVariantChanged;

        LogWrapper.Debug(Module, "[Tooltip] 全局提示服务已启用，接管 ToolTip.Tip");
    }

    public static void Disable()
    {
        if (!_enabled) return;

        _enabled = false;
        _openingSubscription?.Dispose();
        _openingSubscription = null;
        _pressSubscription?.Dispose();
        _pressSubscription = null;
        _releaseSubscription?.Dispose();
        _releaseSubscription = null;
        _pointerPressed = false;

        if (Application.Current is { } application)
            application.ActualThemeVariantChanged -= _OnThemeVariantChanged;

        _Hush();
        LogWrapper.Debug(Module, "[Tooltip] 全局提示服务已停用");
    }

    public static void Dismiss()
    {
        if (_target is null && _flyout is not { IsOpen: true }) return;
        _WindDown();
    }

    private static void _OnToolTipOpening(Control control, CancelRoutedEventArgs e)
    {
        if (!_enabled) return;
        if (_pointerPressed)
        {
            e.Cancel = true;
            e.Handled = true;
            return;
        }

        var content = ToolTip.GetTip(control);
        if (content is null or string { Length: 0 }) return;

        if (!GetIsEnabled(control)) return;
        if (!control.IsEffectivelyEnabled && !ToolTip.GetShowOnDisabled(control)) return;

        e.Cancel = true;
        e.Handled = true;

        if (ReferenceEquals(_target, control) && _flyout is { IsOpen: true })
            return;

        _Hush();
        _target = control;
        _PopUp(control, content);
    }

    private static void _PopUp(Control target, object content)
    {
        if (_pointerPressed) return;
        if (_flyout is null)
            _BuildUi();

        if (_flyout is null || _shell is null) return;

        _AttachPopupTo(target);
        _RenderInside(content);

        _flyout.PlacementTarget = target;
        _flyout.HorizontalOffset = ToolTip.GetHorizontalOffset(target);
        _flyout.VerticalOffset = ToolTip.GetVerticalOffset(target);
        _flyout.Placement = _GetPlacement(target);
        if (_flyout.Placement == PlacementMode.AnchorAndGravity)
            _ApplyFollowPlacement(default);

        target.PointerMoved += _OnTargetPointerMoved;
        target.PointerExited += _OnTargetPointerExited;
        target.DetachedFromVisualTree += _OnTargetDetached;

        _closing = false;
        _shell.Opacity = 0d;
        _scaler!.ScaleX = ScaleClosed;
        _scaler.ScaleY = ScaleClosed;

        try
        {
            _flyout.IsOpen = true;
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, Module, "提示浮层打开失败");
            _Hush();
            return;
        }

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaOpacity(_shell, 1d, AnimLength),
                PCL.Animations.AaScaleTransform(_shell, 1d - ScaleClosed, AnimLength)
            },
            "Tooltip Open");
    }

    /// <summary>
    ///     代码创建的 Popup 不会自动进入逻辑树。挂到目标控件后，PopupRoot 才能解析到
    ///     FluentTheme 中的 PopupRoot 主题，获得透明背景；内置 ToolTip 通过
    ///     <c>Popup.SetPopupParent</c> 完成同样的事情。
    /// </summary>
    private static void _AttachPopupTo(Control target)
    {
        if (_flyout is null || ReferenceEquals(_flyout.Parent, target)) return;

        if (_flyout.Parent is not null)
            ((ISetLogicalParent)_flyout).SetParent(null);

        ((ISetLogicalParent)_flyout).SetParent(target);
    }

    private static void _DetachPopup()
    {
        if (_flyout?.Parent is null) return;
        ((ISetLogicalParent)_flyout).SetParent(null);
    }

    private static PlacementMode _GetPlacement(Control target)
    {
        var placement = ToolTip.GetPlacement(target);

        if (GetFollowCursor(target) && placement == PlacementMode.Pointer)
            return PlacementMode.AnchorAndGravity;

        return placement == PlacementMode.Pointer ? PlacementMode.Bottom : placement;
    }

    private static void _ApplyFollowPlacement(Point point)
    {
        if (_flyout is null) return;

        _flyout.PlacementAnchor = PopupAnchor.TopLeft;
        _flyout.PlacementGravity = PopupGravity.BottomRight;
        _flyout.PlacementRect = new Rect(
            point.X + CursorGap,
            point.Y + CursorGap,
            1d,
            1d);
    }

    private static void _BuildUi()
    {
        _scaler = new ScaleTransform(ScaleClosed, ScaleClosed);
        _shell = new Border
        {
            BorderThickness = new Thickness(0d),
            CornerRadius = new CornerRadius(8d),
            MaxWidth = 700d,
            UseLayoutRounding = true,
            RenderTransform = _scaler,
            RenderTransformOrigin = new RelativePoint(0d, 0d, RelativeUnit.Relative),
            BoxShadow = BoxShadows.Parse("0 0 18 0 #26000000"),
            IsHitTestVisible = false
        };

        var wrap = new Grid
        {
            Margin = new Thickness(ShadowBlur + 1d),
            Background = Brushes.Transparent
        };
        wrap.Children.Add(_shell);

        _flyout = new Popup
        {
            IsHitTestVisible = false,
            WindowManagerAddShadowHint = false,
            Child = wrap
        };

        _ApplyThemeBrushes();
    }

    private static void _ApplyThemeBrushes()
    {
        if (_shell is null) return;

        if (_TryFindBrush("ColorBrushWhite", out var background))
            _shell.Background = background;
        else if (_TryFindBrush("ColorBrushToolTip", out var fallback))
            _shell.Background = fallback;
        _shell.BorderBrush = null;
    }

    private static bool _TryFindBrush(string key, out IBrush? brush)
    {
        brush = null;
        if (Application.Current is not { } application
            || !application.TryFindResource(key, out var value))
            return false;

        brush = value switch
        {
            ISolidColorBrush solid => solid,
            Color color => new SolidColorBrush(color),
            _ => null
        };
        return brush is not null;
    }

    private static void _RenderInside(object content)
    {
        if (_shell is null) return;

        _shell.Child = null;

        if (content is string text)
        {
            var block = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Margin = InnerPad,
                FontSize = TipFontSize,
                LineHeight = TipLineHeight,
                MaxWidth = MaxContentWidth
            };
            if (_TryFindBrush("ColorBrush1", out var foreground))
                block.Foreground = foreground;
            _shell.Child = block;
            return;
        }

        _shell.Child = new ContentPresenter
        {
            Content = content,
            Margin = InnerPad,
            MaxWidth = MaxContentWidth,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
    }

    private static void _OnTargetPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_closing || _flyout is not { IsOpen: true } || _shell is null || _target is null)
            return;
        if (!GetFollowCursor(_target)
            || _flyout.Placement != PlacementMode.AnchorAndGravity) return;

        _ApplyFollowPlacement(e.GetPosition(_target));
    }

    private static void _OnTargetPointerExited(object? sender, PointerEventArgs e)
    {
        _WindDown();
    }

    private static void _OnTargetDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _WindDown();
    }

    private static void _OnThemeVariantChanged(object? sender, EventArgs e)
    {
        _ApplyThemeBrushes();
    }

    private static void _WindDown()
    {
        if (_closing) return;
        _closing = true;

        _DetachTarget();

        if (_flyout is not { IsOpen: true } || _shell is null)
        {
            _Hush();
            return;
        }

        var shell = _shell;
        PCL.Animations.AniStop("Tooltip Open");
        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaOpacity(shell, -shell.Opacity, AnimExit),
                PCL.Animations.AaScaleTransform(
                    shell,
                    ScaleClosed - (_scaler?.ScaleX ?? 1d),
                    AnimExit),
                PCL.Animations.AaCode(_Hush, after: true)
            },
            "Tooltip Close");
    }

    private static void _DetachTarget()
    {
        if (_target is null) return;

        _target.PointerMoved -= _OnTargetPointerMoved;
        _target.PointerExited -= _OnTargetPointerExited;
        _target.DetachedFromVisualTree -= _OnTargetDetached;
        _target = null;
    }

    private static void _Hush()
    {
        PCL.Animations.AniStop("Tooltip Open");
        PCL.Animations.AniStop("Tooltip Close");

        _closing = false;
        _DetachTarget();

        if (_flyout is not null)
            try
            {
                _flyout.IsOpen = false;
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, Module, "提示浮层关闭失败");
            }
            finally
            {
                _DetachPopup();
            }

        if (_shell is not null)
        {
            _shell.Opacity = 1d;
            _shell.Child = null;
        }

        if (_scaler is not null)
        {
            _scaler.ScaleX = 1d;
            _scaler.ScaleY = 1d;
        }
    }
}