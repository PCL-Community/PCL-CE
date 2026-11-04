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
using PCL.Core.UI.Theme;
using PCL.UI.Controls.Common;

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
        using var controlScope = PCL.Animations.SuspendControl();

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
            PCL.Animations.AniStop(DisposeKey);
        };

        PointerReleased += _OnPointerReleased;
        PointerPressed += _OnPointerPressed;
        PointerExited += (_, _) => _isMouseDown = false;
    }

    #region 主题

    private void UpdateUi()
    {
        var hue = Theme switch
        {
            Themes.Blue => 210d,
            Themes.Yellow => 40d,
            _ => 355d
        };

        var tone = ThemeService.CurrentTone;
        var background = new MyColor().FromHsl2(hue, 90d, tone.L7 * 100d);
        var foreground = new MyColor().FromHsl2(hue, 90d, tone.L2 * 100d);

        Background = new SolidColorBrush((Color)background);
        BorderBrush = new SolidColorBrush((Color)foreground);
        _labText.Foreground = new SolidColorBrush((Color)foreground);
        _btnClose.Foreground = new SolidColorBrush((Color)foreground);
        _btnClose.RefreshAnim();

        // 读取 HasBorder 会按当前上边框判断是否补齐其余三边
        HasBorder = HasBorder;
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
            _isClosing = false;
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
    ///     关闭提示条：轻微缩小、淡出、高度收拢后隐藏；动画关闭或已不可交互时立即隐藏。
    /// </summary>
    /// <param name="callBack">收起完成后在 UI 线程执行的回调，参数为本提示条。</param>
    public void AniDispose(Action<MyHint>? callBack = null)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => AniDispose(callBack));
            return;
        }

        if (_isClosing)
            return;

        _isClosing = true;

        PCL.Animations.AniDispose(
            this,
            false,
            _ => callBack?.Invoke(this),
            0.92d,
            true,
            DisposeKey);
    }

    public void Close()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Close);
            return;
        }

        AniDispose();
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