using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.UI.Theme;

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
    private NColor _backColor;

    public MyComboBoxItem()
    {
        Padding = new Thickness(6d, 4d);
        FontSize = 13d;

        PointerEntered += OnPointerEntered;
        PointerExited += (_, _) => RefreshColor();
        PointerMoved += OnMouseMove;
        Loaded += (_, _) => RefreshColor(false);
        ThemeRefreshWatcher.Watch(this, OnThemeColorChanged);
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

        var backColor = new NColor(0f, 0f, 0f, 0f);
        var hasBackColor = !string.IsNullOrEmpty(newBackColorName)
            && InputTheme.TryColor(this, newBackColorName, out backColor);

        var newForeColor = IsEnabled ? InputTheme.Text(this) : InputTheme.TextSecondary(this);

        if (animate
            && InputAnimation.CanAnimate(this)
            && _backColorName == newBackColorName
            && (!hasBackColor || _backColor == backColor)
            && Math.Abs(Opacity - newFontOpacity) < 0.0001d
            && Foreground == newForeColor)
            return;

        _backColorName = newBackColorName;
        _backColor = hasBackColor ? backColor : default;

        if (animate && InputAnimation.CanAnimate(this))
        {
            var fromColor = InputAnimation.ReadColor(this, BackgroundProperty);
            var targetColor = hasBackColor ? backColor : new NColor(0f, fromColor);
            if (hasBackColor && fromColor.A <= 0f)
                fromColor = new NColor(0f, backColor);

            var animations = new List<IAnimation>();
            if (fromColor != targetColor)
            {
                animations.Add(new NColorFromToAnimation
                {
                    Target = new AvaloniaAnimatable(this, BackgroundProperty),
                    From = fromColor,
                    HasFrom = true,
                    To = targetColor,
                    Duration = TimeSpan.FromMilliseconds(time)
                });
            }

            if (Math.Abs(newFontOpacity - Opacity) >= 0.0001d)
                animations.Add(new OpacityAnimation(this, newFontOpacity - Opacity, time));

            if (animations.Count > 0)
                AnimationService.Run(animations, ColorKey);
            else
                AnimationService.CancelAnimationByName(ColorKey);
        }
        else
        {
            AnimationService.CancelAnimationByName(ColorKey);
            Background = hasBackColor ? backColor.ToBrush() : Brushes.Transparent;
            Opacity = newFontOpacity;
        }

        Foreground = newForeColor;
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
        // 仅切换主题色（不改深浅模式）时不会触发 ActualThemeVariantChanged，需监听 ColorThemeChanged
        ThemeService.ColorThemeChanged += OnColorThemeChanged;
        RefreshColor(false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        ThemeService.ColorThemeChanged -= OnColorThemeChanged;
        AnimationService.CancelAnimationByName(ColorKey);
        base.OnDetachedFromVisualTree(e);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        RefreshColor(false);
    }

    private void OnColorThemeChanged(ColorTheme theme)
    {
        RefreshColor(false);
    }

    private void OnThemeColorChanged()
    {
        _backColorName = null;
        RefreshColor(false);
    }
}
