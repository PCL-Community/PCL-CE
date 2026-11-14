using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.UI.Controls;

/// <summary>
///     下拉框列表项：内容转字符串、隐式字符串转换与选中/悬停/禁用配色。
///     键盘与指针高亮分别按焦点与悬停状态近似；背景色与透明度经 新动画引擎 过渡，
///     动画关闭时直接落到终值。
/// </summary>
public class MyComboBoxItem : ComboBoxItem
{
    private const int AnimationTimeIn = 100;
    private const int AnimationTimeOut = 300;

    // 基础

    public int Uuid = InputTheme.NewUuid();

    private string? _backColorName;
    private double _fontOpacity = 1d;

    public MyComboBoxItem()
    {
        Padding = new Thickness(6d, 4d);
        FontSize = 13d;

        PointerEntered += OnPointerEntered;
        PointerExited += (_, _) => RefreshColor();
        PointerMoved += OnMouseMove;
        Loaded += (_, _) => RefreshColor(false);
    }

    private string ColorKey => InputAnimation.Key("MyComboBoxItem Color", Uuid);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsSelectedProperty
            || change.Property == IsEnabledProperty
            || change.Property == IsFocusedProperty)
            RefreshColor();
    }

    /// <summary>
    ///     重新计算当前项的背景色与透明度。
    /// </summary>
    /// <param name="animate">是否播放配色过渡；首次挂载或主题切换时传 <c>false</c>。</param>
    public void RefreshColor(bool animate = true)
    {
        // Avalonia 的 ComboBoxItem 没有 WPF 的 IsHighlighted：键盘模式下用焦点近似键盘高亮，
        // 指针模式下用悬停近似指针高亮；两种来源由 MyComboBox.IsKeyboardNavigation 区分。
        var keyboardNavigation =
            ItemsControl.ItemsControlFromItemContainer(this) is MyComboBox
            {
                IsKeyboardNavigation: true
            };

        string newBackColorName;
        double newFontOpacity;
        int time;

        if (IsSelected)
        {
            newBackColorName = InputTheme.ComboSelectedKey;
            newFontOpacity = 1d;
            time = AnimationTimeIn;
        }
        else if (keyboardNavigation ? IsFocused : IsPointerOver)
        {
            newBackColorName = InputTheme.ComboHoverKey;
            newFontOpacity = 1d;
            time = AnimationTimeIn;
        }
        else if (IsEnabled)
        {
            newBackColorName = string.Empty;
            newFontOpacity = 1d;
            time = AnimationTimeOut;
        }
        else
        {
            newBackColorName = string.Empty;
            newFontOpacity = 0.4d;
            time = AnimationTimeOut;
        }

        if (_backColorName == newBackColorName && Math.Abs(_fontOpacity - newFontOpacity) < 0.0001d)
            return;

        _backColorName = newBackColorName;
        _fontOpacity = newFontOpacity;

        var targetColor = string.IsNullOrEmpty(newBackColorName)
            ? new NColor(0d, 0d, 0d, 0d)
            : InputTheme.TryColor(this, newBackColorName, out var resolved)
                ? resolved
                : new NColor(0d, 0d, 0d, 0d);

        if (animate && InputAnimation.CanAnimate(this))
        {
            AnimationService.Run(
                new List<IAnimation>
                {
                    new ColorAnimation(
                        this,
                        BackgroundProperty,
                        targetColor - InputAnimation.ReadColor(this, BackgroundProperty),
                        time),
                    new OpacityAnimation(this, newFontOpacity - Opacity, time)
                },
                ColorKey);
        }
        else
        {
            AnimationService.CancelAnimationByName(ColorKey);
            Background = string.IsNullOrEmpty(newBackColorName)
                ? Brushes.Transparent
                : InputTheme.Brush(this, newBackColorName, Brushes.Transparent);
            Opacity = newFontOpacity;
        }

        Foreground = IsEnabled ? InputTheme.Text(this) : InputTheme.TextSecondary(this);
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        NotifyComboPointerActivity();
        RefreshColor();
    }

    private void OnMouseMove(object? sender, PointerEventArgs e)
    {
        NotifyComboPointerActivity();
    }

    /// <summary>
    ///     指针进入列表项时通知所属下拉框切回指针高亮；下拉框会刷新全部已生成项。
    /// </summary>
    private void NotifyComboPointerActivity()
    {
        if (ItemsControl.ItemsControlFromItemContainer(this) is MyComboBox box)
            box.NotifyPointerActivity();
    }

    public override string ToString()
    {
        return Content?.ToString() ?? string.Empty;
    }

    public static implicit operator string(MyComboBoxItem value)
    {
        return value.Content?.ToString() ?? string.Empty;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        RefreshColor(false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        AnimationService.CancelAnimationByName(ColorKey);
        base.OnDetachedFromVisualTree(e);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        _backColorName = null;
        RefreshColor(false);
    }
}