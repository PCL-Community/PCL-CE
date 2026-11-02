using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;

namespace PCL.UI.Controls;

/// <summary>
///     内联提示条：左侧色条按 <see cref="Theme" /> 取红/黄/蓝三档颜色，正文支持
///     <see cref="Inlines" /> 富文本；可选的关闭按钮会写入 <see cref="RelativeSetup" />
///     对应的配置项并收起提示条。
/// </summary>
public class MyHint : Border
{
    public enum Themes
    {
        Blue = 0,
        Red = 1,
        Yellow = 2
    }

    public static readonly StyledProperty<bool> IsWarnProperty =
        AvaloniaProperty.Register<MyHint, bool>("IsWarn", true);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyHint, string>(nameof(Text), string.Empty);

    private static int _uuidSeed;
    private readonly MyIconButton _btnClose;

    private readonly TextBlock _labText;

    public int Uuid = Interlocked.Increment(ref _uuidSeed);
    private bool _isClosing;

    private bool _isMouseDown;
    private Action? _unwatchTheme;

    public MyHint()
    {
        CornerRadius = new CornerRadius(2d);
        BorderThickness = new Thickness(3d, 0d, 0d, 0d);
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _labText = new TextBlock
        {
            Name = "LabText",
            IsHitTestVisible = false,
            LineHeight = 16d,
            Margin = new Thickness(0d, 0d, 0d, -1d),
            Padding = new Thickness(12d, 9d),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        _btnClose = new MyIconButton
        {
            Name = "BtnClose",
            Theme = MyIconButton.Themes.Custom,
            SvgIcon = "lucide/x",
            Width = 20d,
            Height = 20d,
            Margin = new Thickness(0d, 0d, 8d, 0d),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Focusable = false,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(_btnClose, Lang.Text("Common.Hint.DoNotShowAgain"));
        _btnClose.Click += (_, _) => _OnCloseClick();

        var grid = new Grid
        {
            UseLayoutRounding = true
        };
        grid.ColumnDefinitions = new ColumnDefinitions("*,Auto");
        grid.Children.Add(_labText);
        Grid.SetColumn(_btnClose, 1);
        grid.Children.Add(_btnClose);

        Child = grid;
        FeedbackTheme.AttachStyles(this);

        UpdateUi();
        Loaded += (_, _) =>
        {
            UpdateUi();
            _unwatchTheme?.Invoke();
            _unwatchTheme = FeedbackTheme.WatchTheme(this, UpdateUi);
            _OnLoaded();
        };
        Unloaded += (_, _) =>
        {
            _unwatchTheme?.Invoke();
            _unwatchTheme = null;
            PCL.Animations.AniStop(ExpandKey);
            PCL.Animations.AniStop(DisposeKey);
            // 停掉动画后恢复自然高度与不透明度，避免控件复用时停留在过渡中间态。
            Height = double.NaN;
            Opacity = 1d;
        };

        PointerReleased += _OnPointerReleased;
        PointerPressed += _OnPointerPressed;
        PointerExited += (_, _) => _isMouseDown = false;
    }

    #region 主题

    private void UpdateUi()
    {
        var accentKey = Theme switch
        {
            Themes.Blue => "PageBrushAccent",
            Themes.Yellow => "PageBrushWarn",
            _ => "PageBrushError"
        };
        var fallback = Theme switch
        {
            Themes.Blue => Color.FromRgb(0x00, 0x78, 0xD4),
            Themes.Yellow => Color.FromRgb(0xC8, 0x8A, 0x00),
            _ => Color.FromRgb(0xD1, 0x35, 0x35)
        };

        var accent = FeedbackTheme.GetColor(this, accentKey, fallback);
        var isDark = FeedbackTheme.IsDarkTheme(this);
        Background = FeedbackTheme.Tint(accent, isDark ? 0.20d : 0.10d);
        BorderBrush = new SolidColorBrush(accent);
        _labText.Foreground = new SolidColorBrush(accent);
        _btnClose.Foreground = new SolidColorBrush(accent);
        _btnClose.RefreshAnim();

        // 读取 HasBorder 会按当前上边框判断是否补齐其余三边
        HasBorder = HasBorder;
    }

    #endregion

    #region 展开

    /// <summary>
    ///     从隐藏切换为可见时的高度与透明度展开，避免显隐生硬。
    /// </summary>
    private void _AnimateExpand()
    {
        if (!IsVisible)
            return;

        // 允许提示条在关闭过程中被重新显示。
        _isClosing = false;
        IsHitTestVisible = true;

        if (!IsLoaded || !FeedbackAnimation.CanAnimate(this))
        {
            Height = double.NaN;
            Opacity = 1d;
            return;
        }

        var savedHeight = Height;
        Height = double.NaN;
        // 用当前已排版的宽度测量，保证换行后的自然高度而不是单行高度。
        var availableWidth = Bounds.Width > 0d ? Bounds.Width : double.PositiveInfinity;
        Measure(new Size(availableWidth, double.PositiveInfinity));
        var targetHeight = DesiredSize.Height;
        Height = savedHeight;

        if (targetHeight <= 1d)
        {
            Height = double.NaN;
            Opacity = 1d;
            return;
        }

        PCL.Animations.AniStop(DisposeKey);
        Height = 0d;
        Opacity = 0d;
        if (RenderTransform is not ScaleTransform)
        {
            RenderTransformOrigin = RelativePoint.Center;
            RenderTransform = new ScaleTransform(0.96d, 0.96d);
        }

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaHeight(
                    this,
                    targetHeight,
                    220,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaOpacity(
                    this,
                    1d,
                    220,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaScaleTransform(
                    this,
                    1d - FeedbackAnimation.CurrentScale(this),
                    220,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaCode(() => Height = double.NaN, after: true)
            },
            ExpandKey);
    }

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsWarnProperty && change.NewValue is bool isWarn)
            Theme = isWarn ? Themes.Red : Themes.Blue;
        else if (change.Property == TextProperty)
            _labText.Text = change.NewValue as string ?? string.Empty;
        else if (change.Property == IsVisibleProperty && change.NewValue is true)
            Dispatcher.UIThread.Post(_AnimateExpand);
    }

    #region 边框

    public bool HasBorder
    {
        get => BorderThickness.Top > 0d;
        set =>
            BorderThickness = value
                ? new Thickness(3d, 1d, 1d, 1d)
                : new Thickness(3d, 0d, 0d, 0d);
    }

    private Themes _theme = Themes.Red;

    public new Themes Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            UpdateUi();
        }
    }

    [Obsolete("请改用 Theme 属性。")]
    public bool IsWarn
    {
        get => GetValue(IsWarnProperty);
        set => SetValue(IsWarnProperty, value);
    }

    #endregion

    #region 文本

    /// <summary>
    ///     提示条正文的内联集合；XAML 中直接写在 <c>MyHint</c> 内的文本作为内联内容。
    /// </summary>
    [Content]
    public InlineCollection Inlines => _labText.Inlines!;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    #endregion

    #region 关闭按钮

    public bool CanClose
    {
        get => _btnClose.IsVisible;
        set => _btnClose.IsVisible = value;
    }

    /// <summary>
    ///     关联的配置项键；配置项已存在用户值时不再显示提示条，点击关闭按钮会写入该配置项。
    /// </summary>
    public string RelativeSetup { get; set; } = string.Empty;

    private string ExpandKey => FeedbackAnimation.Name("MyHint Expand", Uuid);

    private string DisposeKey => FeedbackAnimation.Name("MyHint Dispose", Uuid);

    private void _OnLoaded()
    {
        if (CanClose
            && !string.IsNullOrEmpty(RelativeSetup)
            && ConfigService.TryGetConfigItemNoType(RelativeSetup, out var item)
            && item.GetValueNoType() is not null)
            IsVisible = false;
    }

    private void _OnCloseClick()
    {
        if (!string.IsNullOrEmpty(RelativeSetup)
            && ConfigService.TryGetConfigItemNoType(RelativeSetup, out var item))
            item.SetValueNoType(true);

        Close();
    }

    /// <summary>
    ///     关闭提示条：轻微缩小、淡出、高度收拢后隐藏。动画关闭时立即隐藏。
    /// </summary>
    public void Close()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Close);
            return;
        }

        if (_isClosing)
            return;

        _isClosing = true;
        PCL.Animations.AniStop(ExpandKey);
        if (!IsHitTestVisible)
        {
            IsVisible = false;
            return;
        }

        IsHitTestVisible = false;

        if (!FeedbackAnimation.CanAnimate(this))
        {
            IsVisible = false;
            return;
        }

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaScaleTransform(
                    this,
                    -0.08d,
                    200,
                    ease: new PCL.Animations.AniEaseInFluent()),
                PCL.Animations.AaOpacity(
                    this,
                    -Opacity,
                    200,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaHeight(
                    this,
                    -Bounds.Height,
                    150,
                    100,
                    new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaCode(() => IsVisible = false, after: true)
            },
            DisposeKey);
    }

    #endregion

    #region 点击

    private void _OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || !_isMouseDown)
            return;

        _isMouseDown = false;
        e.Handled = true;
        CustomEventService.Raise(this);
    }

    private void _OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            _isMouseDown = true;
    }

    #endregion
}