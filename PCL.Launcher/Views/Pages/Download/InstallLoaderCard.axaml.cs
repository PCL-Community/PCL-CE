using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

/// <summary>
///     下载安装页与实例修改页的加载器卡片：固定卡片几何与头部结构，卡片标题、状态图标、清空按钮和
///     版本行容器均由 XAML 提供，动态版本行仍由 <see cref="InstallLoaderSelector" /> 生成。
///     与 WPF 一致：卡片带高度动画，清空按钮为 10px 叉号，颜色跟随卡片标题。
/// </summary>
public partial class InstallLoaderCard : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<InstallLoaderCard, string>(nameof(Title), string.Empty);

    private bool? _headerShown;
    private bool _clearBrushBound;
    private bool _isClearMouseDown;

    public InstallLoaderCard()
    {
        InitializeComponent();
        CardRoot.SwapControl = BodyHost;
        CardRoot.IsSwapped = true;
        CardRoot.PropertyChanged += (_, e) =>
        {
            if (e.Property == MyCard.IsSwappedProperty)
                SyncCollapsedHeader(CardRoot.IsSwapped);
        };

        BtnClear.PointerPressed += OnClearPointerPressed;
        BtnClear.PointerReleased += OnClearPointerReleased;
        BtnClear.PointerExited += (_, _) => _isClearMouseDown = false;
        CardRoot.Loaded += (_, _) => BindClearBrush();
    }

    /// <summary>点击清空按钮。</summary>
    public event EventHandler? Clear;

    public MyCard Card => CardRoot;

    public StackPanel Body => BodyHost;

    public MyImage Logo => HeaderLogo;

    public TextBlock Info => StatusText;

    public Control ClearButton => BtnClear;

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty && CardRoot is not null)
            CardRoot.SetCurrentValue(
                MyCard.TitleProperty,
                change.NewValue as string ?? string.Empty);
    }

    /// <summary>与 WPF 相同：叉号颜色绑定到卡片标题，悬停卡片时一起变为强调色。</summary>
    private void BindClearBrush()
    {
        if (_clearBrushBound)
            return;
        _clearBrushBound = true;
        BtnClearInner.Bind(
            Shape.FillProperty,
            new Binding(nameof(TextBlock.Foreground))
            {
                Source = CardRoot.MainTextBlock,
                Mode = BindingMode.OneWay
            });
    }

    private void OnClearPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(BtnClear).Properties.IsLeftButtonPressed)
            return;
        _isClearMouseDown = true;
        // 不让卡片记下这次按下，否则松开后卡片会残留折叠/自定义事件标记
        e.Handled = true;
    }

    private void OnClearPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // 标记为已处理，避免卡片把这次点击当成展开/折叠
        e.Handled = true;
        if (!_isClearMouseDown)
            return;
        _isClearMouseDown = false;
        Clear?.Invoke(this, EventArgs.Empty);
    }

    public void SetLogo(string? source, double height, Thickness margin)
    {
        HeaderLogo.Height = height;
        HeaderLogo.Margin = margin;
        HeaderLogo.IsVisible = !string.IsNullOrEmpty(source);
        if (!string.IsNullOrEmpty(source))
            HeaderLogo.Source = source;
    }

    public void SetBodyMargin(Thickness margin)
    {
        BodyHost.Margin = margin;
    }

    /// <summary>与 WPF 一致：未选择时为 Gray4，已选择时为 Gray1。</summary>
    public void SetStatusSelected(bool selected)
    {
        ContainerTheme.BindDynamic(
            StatusText,
            TextBlock.ForegroundProperty,
            selected ? "ColorBrushGray1" : "ColorBrushGray4");
    }

    /// <summary>
    ///     折叠时显示头部版本，展开时淡出。状态没变则不重复播放。
    ///     与 WPF 的 SetPanelVisibility 相同：出现 150 ms 上移 + 60 ms 淡入，消失 60 ms 下移淡出。
    /// </summary>
    public void SyncCollapsedHeader(bool shown)
    {
        if (_headerShown == shown)
            return;
        var animate = _headerShown is not null && ContainerAnimation.CanAnimate(this);
        _headerShown = shown;
        var transform = HeaderGrid.RenderTransform as TranslateTransform;
        if (!animate)
        {
            AnimationService.CancelAnimationByName(HeaderAnimationKey);
            HeaderGrid.Opacity = shown ? 1d : 0d;
            HeaderGrid.RenderTransform = shown ? null : new TranslateTransform(0d, 6d);
            return;
        }

        if (transform is null)
        {
            transform = new TranslateTransform();
            HeaderGrid.RenderTransform = transform;
        }

        AnimationService.Run(
            shown
                ? new List<IAnimation>
                {
                    new TranslateYAnimation(
                        HeaderGrid,
                        -transform.Y,
                        150,
                        ease: new FluentEaseOut()),
                    new OpacityAnimation(HeaderGrid, 1d - HeaderGrid.Opacity, 60)
                }
                : new List<IAnimation>
                {
                    new TranslateYAnimation(HeaderGrid, 6d - transform.Y, 60),
                    new OpacityAnimation(HeaderGrid, -HeaderGrid.Opacity, 60)
                },
            HeaderAnimationKey);
    }

    private string HeaderAnimationKey => "InstallLoaderCard Header " + GetHashCode();
}
