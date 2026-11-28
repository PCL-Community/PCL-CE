using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace PCL.UI.Controls;

/// <summary>标题栏与筛选胶囊共用的滑动选中块。</summary>
internal sealed class RadioButtonSelectionIndicator
{
    private readonly Panel _buttons;
    private readonly Border _indicator;
    private readonly Visual _coordinateHost;
    private readonly TranslateTransform _transform = new();
    private readonly string _animationKey = $"RadioButton Indicator {InputTheme.NewUuid()}";
    private MyRadioButton? _target;
    private Rect _bounds;

    public RadioButtonSelectionIndicator(Panel buttons, Border indicator, Visual coordinateHost)
    {
        _buttons = buttons;
        _indicator = indicator;
        _coordinateHost = coordinateHost;
        _indicator.RenderTransform = _transform;
        _indicator.IsVisible = false;
        _buttons.Loaded += (_, _) => Update();
        _buttons.LayoutUpdated += (_, _) => Update();
        _buttons.Unloaded += (_, _) => Reset();
    }

    public void Update()
    {
        var target = _buttons.Children.OfType<MyRadioButton>()
            .FirstOrDefault(button => button.Checked && button.IsVisible);
        if (target is null)
        {
            Reset();
            return;
        }
        if (target.Bounds.Width <= 0d
            || target.TranslatePoint(default, _coordinateHost) is not { } point)
            return;

        var bounds = new Rect(point, target.Bounds.Size);
        // 比较目标几何而非动画中的位移，避免布局事件反复重启动画。
        if (_target == target && _bounds == bounds)
            return;
        var animate = _target is not null && _target != target && InputAnimation.CanAnimate(_buttons);
        _target = target;
        _bounds = bounds;
        _indicator.Width = bounds.Width;
        _indicator.IsVisible = true;

        AnimationService.CancelAnimationByName(_animationKey);
        var delta = bounds.X - _transform.X;
        if (animate && Math.Abs(delta) >= 0.1d)
            AnimationService.Run(
                new TranslateXAnimation(_indicator, delta, 240, ease: new FluentEaseOut(EasePower.Weak)),
                _animationKey);
        else
            _transform.X = bounds.X;
    }

    private void Reset()
    {
        AnimationService.CancelAnimationByName(_animationKey);
        _indicator.IsVisible = false;
        _target = null;
    }
}
