using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.Logging;
using PCL.UI.Controls.Common;
using Path = Avalonia.Controls.Shapes.Path;

namespace PCL.UI.Controls;

/// <summary>
///     卡片容器：阴影层 + 圆角背景层 + 标题/折叠箭头层，用户内容直接加入 <see cref="Panel.Children" />。
///     悬停阴影与标题变色、主题配色过渡、内容高度变化与折叠箭头旋转由 新动画引擎 驱动；
///     高度动画带 <see cref="UseAnimation" /> 与 <c>_isHeightAnimating</c> 双重守卫，避免尺寸回调自激。
/// </summary>
public class MyCard : Grid
{
    private const double DropShadowIdleOpacity = 0.07d;
    private const double DropShadowHoverOpacity = 0.4d;

    private const int TitleColorTime = 90;
    private const int ShadowColorTime = 90;
    private const int ThemeColorTime = 300;

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MyCard, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MyCard, CornerRadius>(
            nameof(CornerRadius),
            new CornerRadius(5d));

    public static readonly StyledProperty<bool> IsSwappedProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(IsSwapped));

    public static readonly StyledProperty<IBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<MyCard, IBrush?>(nameof(BackgroundBrush));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyCard, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyCard, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<ICommand?> PreviewSwapCommandProperty =
        AvaloniaProperty.Register<MyCard, ICommand?>(nameof(PreviewSwapCommand));

    private static int _uuidSeed;

    private readonly BlurBorder _mainBorder;
    private readonly Grid _mainGrid;

    public readonly int uuid = Interlocked.Increment(ref _uuidSeed);
    private bool _isCustomMouseDown; // 用于触发自定义事件的 MouseDown
    private bool _isLoad;

    private bool _isSwapMouseDown; // 用于触发卡片展开/折叠的 MouseDown
    private Path? _mainSwap;

    private TextBlock? _mainTextBlock;

    public MyCard()
    {
        MainChrome = new MyDropShadow
        {
            Margin = new Thickness(-3d, -3d, -3d, -4d),
            ShadowRadius = 3d,
            CornerRadius = new CornerRadius(5d),
            CenterTransparent = true,
            Opacity = DropShadowIdleOpacity,
            IsHitTestVisible = false
        };
        ContainerTheme.BindDynamic(MainChrome, MyDropShadow.ColorProperty, "ColorObject1");
        Children.Insert(0, MainChrome);
        _mainBorder = new BlurBorder
        {
            CornerRadius = new CornerRadius(5d),
            // BorderChild 是页面内容的宿主；必须让内容参与命中测试，
            // 否则所有通过 BorderChild 挂载的按钮/输入框都会变成不可点击。
            IsHitTestVisible = true
        };
        Children.Insert(1, _mainBorder);
        _mainGrid = new Grid();
        Children.Add(_mainGrid);

        Loaded += (_, _) => Init();
        SizeChanged += _OnSizeChanged;
        PointerEntered += _OnPointerEntered;
        PointerExited += _OnPointerExited;
        PointerPressed += _OnPointerPressed;
        PointerReleased += _OnPointerReleased;
    }

    public MyDropShadow MainChrome { get; }

    public Control? BorderChild
    {
        get => _mainBorder.Child;
        set => _mainBorder.Child = value;
    }

    public TextBlock MainTextBlock
    {
        get
        {
            // 父级的 Loaded 先于本卡片触发时标题字块还没创建，这里先补一次初始化。
            Init();
            return _mainTextBlock!;
        }
        set => _mainTextBlock = value;
    }

    public Path? MainSwap
    {
        get
        {
            Init();
            return _mainSwap;
        }
        set => _mainSwap = value;
    }

    /// <summary>
    ///     标题文本的内联集合。
    /// </summary>
    public InlineCollection Inlines => MainTextBlock.Inlines!;

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public IBrush? BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    /// <summary>
    ///     是否启用鼠标悬停反馈（阴影浓度 90 ms 过渡）。
    /// </summary>
    public bool HasMouseAnimation { get; set; } = true;

    /// <summary>
    ///     是否启用内容高度改变动画。折叠栏开合期间会被临时关闭，避免与父卡片的高度动画互相干扰。
    /// </summary>
    public bool UseAnimation { get; set; } = true;

    private void Init()
    {
        if (_isLoad)
            return;
        _isLoad = true;

        using var controlScope = AnimationService.SuspendControl();

        if (_mainTextBlock is null)
        {
            _mainTextBlock = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(15d, 12d, 0d, 0d),
                FontWeight = FontWeight.Bold,
                FontSize = 13d,
                IsHitTestVisible = false,
                Foreground = _ResolveTextBrush(),
                Text = Title
            };
            _mainGrid.Children.Add(_mainTextBlock);
        }

        if (CanSwap || SwapControl is not null)
        {
            if (SwapControl is null && Children.Count > 3)
                SwapControl = Children[3];

            if (_mainSwap is null && SwapControl is not null)
            {
                _mainSwap = new Path
                {
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Stretch = Stretch.Uniform,
                    Height = 6d,
                    Width = 10d,
                    Margin = new Thickness(0d, 17d, 16d, 0d),
                    Data = Geometry.Parse("M2,4 l-2,2 10,10 10,-10 -2,-2 -8,8 -8,-8 z"),
                    RenderTransform = new RotateTransform(180d),
                    RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative),
                    Fill = _ResolveTextBrush()
                };
                _mainGrid.Children.Add(_mainSwap);
            }
        }

        // 改变默认的折叠
        if (IsSwapped && SwapControl is not null)
        {
            _mainSwap?.RenderTransform = new RotateTransform(SwapLogoRight ? 270d : 0d);
            SwapControl.IsVisible = false;
            Height = SwapedHeight;
            // 取消由高度变化被迫触发的高度动画
            AnimationService.CancelAnimationByName(HeightAnimationKey);
            _isHeightAnimating = false;
            ApplyLayoutClip();
        }
    }

    public void StackInstall()
    {
        var stack = SwapControl as StackPanel;
        if (stack is null)
            return;

        StackInstall(ref stack, InstallMethod);
        SwapControl = stack;
        TriggerForceResize();
    }

    public static void StackInstall(ref StackPanel stack, Action<StackPanel>? installMethod)
    {
        if (stack.Tag is null)
            return;

        try
        {
            installMethod?.Invoke(stack);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "MyCard", "[MyCard] InstallMethod 调用失败");
        }

        stack.Children.Add(
            new Border
            {
                Height = 18d
            }); // 下边距，同时适应折叠
        stack.Tag = null;
    }

    /// <summary>
    ///     通知 MyCard，控件内容已改变，需要中断动画并瞬间更新高度。
    /// </summary>
    public void TriggerForceResize()
    {
        Height = IsSwapped ? SwapedHeight : double.NaN;
        AnimationService.CancelAnimationByName(HeightAnimationKey);
        _isHeightAnimating = false;
        ApplyLayoutClip();
    }

    #region 高度改变动画

    private bool _isHeightAnimating;
    private double _actualUsedHeight; // 回滚实际高度（例如 NaN）

    private void _OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isDisposing || !UseAnimation || !ContainerAnimation.IsEnabled)
            return;

        var deltaHeight = (IsSwapped ? SwapedHeight : e.NewSize.Height) - e.PreviousSize.Height;
        // 首次出现的高度变化由页面切换动画负责，这里只处理内容增减
        if (e.PreviousSize.Height == 0d
            || _isHeightAnimating
            || Math.Abs(deltaHeight) < 1d
            || Bounds.Height == 0d)
            return;

        StartHeightAnimation(deltaHeight, e.PreviousSize.Height);
    }

    /// <summary>
    ///     启动卡片高度变化的动画效果。
    ///     根据变化距离的大小采用不同策略：短距离用简单缓动，长距离拆成匀速段 + 减速段。
    /// </summary>
    /// <param name="delta">高度变化量。</param>
    /// <param name="previousHeight">之前的高度。</param>
    private void StartHeightAnimation(double delta, double previousHeight, double? endHeight = null)
    {
        if (_isHeightAnimating || !ContainerAnimation.CanAnimate(this))
            return;

        var animList = new List<IAnimation>();
        var absDelta = Math.Abs(delta);

        if (absDelta <= 800d)
        {
            // 短距离，直接使用 150 ms 的缓动动画
            animList.Add(
                new HeightAnimation(
                    this,
                    delta,
                    150,
                    ease: new FluentEaseOut(EasePower.ExtraStrong)));
        }
        else
        {
            int easeLength;
            int easeTime;
            int initSpeed; // 到达缓动区前的初速度
            switch (delta)
            {
                case < 0d when absDelta > 5000d * 0.1d:
                    // 收回距离过长（>0.1 s），强制以 100 ms 完成匀速段，然后让减速段更长
                    easeLength = 200;
                    easeTime = 150;
                    initSpeed = (int)Math.Round((absDelta - easeLength) / 0.1d);
                    break;

                case > 0d when absDelta > 5000d * 0.6d:
                    // 展开距离过长（>0.6 s），以 5000 速度展示 300 ms 匀速段，剩下的距离全部归入减速段
                    initSpeed = 5000;
                    easeLength = (int)Math.Round(absDelta - initSpeed * 0.3d);
                    easeTime = 400;
                    break;

                default:
                    // 中程，匀速地快速展开（或收回）
                    easeLength = 150;
                    easeTime = 200;
                    initSpeed = 4000;
                    break;
            }

            // 匀速段
            animList.Add(
                new HeightAnimation(
                    this,
                    (absDelta - easeLength) * Math.Sign(delta),
                    (int)Math.Round((absDelta - easeLength) / initSpeed * 1000d)));
            // 减速段
            animList.Add(
                new HeightAnimation(
                    this,
                    easeLength * Math.Sign(delta),
                    easeTime,
                    ease: new FluentEaseOutWithInitial(initSpeed, easeTime / 1000d, easeLength),
                    after: true));
        }

        animList.Add(
            new ActionAnimation(
                () =>
                {
                    _isHeightAnimating = false;
                    Height = _actualUsedHeight;
                    ApplyLayoutClip();
                    if (IsSwapped && SwapControl is not null)
                        SwapControl.IsVisible = false;
                },
                after: true));
        _isHeightAnimating = true;
        _actualUsedHeight = endHeight ?? (IsSwapped ? SwapedHeight : Height);
        Height = previousHeight;
        ApplyLayoutClip();
        AnimationService.Run(animList, HeightAnimationKey);
    }

    private void ApplyLayoutClip()
    {
        // 只在高度动画期间裁。折叠静止时内容已隐藏，阴影仍要画出卡片外。
        var clip = _isHeightAnimating && !double.IsNaN(Height);
        if (ClipToBounds != clip)
            ClipToBounds = clip;
    }

    private double MeasureExpandedHeight()
    {
        var width = Bounds.Width > 0d ? Bounds.Width : double.PositiveInfinity;
        var savedHeight = Height;
        var savedAnimating = _isHeightAnimating;
        _isHeightAnimating = true;
        try
        {
            Height = double.NaN;
            Measure(new Size(width, double.PositiveInfinity));
            return Math.Max(DesiredSize.Height, SwapedHeight);
        }
        finally
        {
            Height = savedHeight;
            _isHeightAnimating = savedAnimating;
        }
    }

    #endregion

    #region 折叠

    // 若设置了 CanSwap，或 SwapControl 不为空，则判定为会进行折叠。
    // 这是因为不能直接在 XAML 中设置 SwapControl。
    public Control? SwapControl;
    public bool CanSwap { get; set; }

    /// <summary>
    ///     数据转为列表项的转换方法。
    /// </summary>
    public Action<StackPanel>? InstallMethod { get; set; }

    /// <summary>
    ///     是否已被折叠。
    /// </summary>
    public bool IsSwapped
    {
        get => GetValue(IsSwappedProperty);
        set => SetValue(IsSwappedProperty, value);
    }

    /// <summary>
    ///     是否已被折叠。(已过时，请使用 IsSwapped)
    /// </summary>
    [Obsolete("请使用 IsSwapped 属性，IsSwaped 存在拼写错误")]
    public bool IsSwaped
    {
        get => IsSwapped;
        set => IsSwapped = value;
    }

    public bool SwapLogoRight { get; set; }

    public event PreviewSwapEventHandler? PreviewSwap;

    public delegate void PreviewSwapEventHandler(object sender, RouteEventArgs e);

    public event SwapEventHandler? Swap;

    public delegate void SwapEventHandler(object sender, RouteEventArgs e);

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public ICommand? PreviewSwapCommand
    {
        get => GetValue(PreviewSwapCommandProperty);
        set => SetValue(PreviewSwapCommandProperty, value);
    }

    public const int SwapedHeight = 40;

    private void _ApplyTitle(string? value)
    {
        if (_mainTextBlock is not null)
            MainTextBlock.Text = value ?? string.Empty;
    }

    private void _ApplyCornerRadius(CornerRadius value)
    {
        MainChrome.CornerRadius = value;
        _mainBorder.CornerRadius = value;
    }

    private void _ApplyBackgroundBrush(IBrush? value)
    {
        AnimateBrush(
            _mainBorder,
            Border.BackgroundProperty,
            value ?? _ResolveCardBrush(),
            ThemeColorTime,
            true,
            BackgroundColorKey);
    }

    private void _ApplyIsSwapped(bool value)
    {
        if (SwapControl is null)
            return;

        // 当卡片展开时，如果 SwapControl 是 StackPanel 类型，则执行安装方法。
        if (!value && SwapControl is StackPanel stack)
        {
            StackInstall(ref stack, InstallMethod);
            SwapControl = stack;
        }

        // 未加载时由 Loaded 处理无动画折叠，这里直接返回。
        if (!IsLoaded)
        {
            SwapControl.IsVisible = !value;
            Height = value ? SwapedHeight : double.NaN;
            return;
        }

        // 展开和收起都先露出内容，再按原来的分段高度动画从当前高度走到目标高度。
        // 动画期间 Height 保持显式值，ApplyLayoutClip 会把内容裁在卡片里。
        AnimationService.CancelAnimationByName(HeightAnimationKey);
        _isHeightAnimating = false;
        ApplyLayoutClip();
        SwapControl.IsVisible = true;
        if (!UseAnimation || !ContainerAnimation.CanAnimate(this))
        {
            SwapControl.IsVisible = !value;
            TriggerForceResize();
        }
        else
        {
            var start = Bounds.Height > 0d ? Bounds.Height : SwapedHeight;
            if (value)
                StartHeightAnimation(SwapedHeight - start, start, SwapedHeight);
            else
            {
                var full = MeasureExpandedHeight();
                StartHeightAnimation(full - start, start, double.NaN);
            }
        }

        // 根据折叠状态旋转箭头图标：折叠时指向右侧或向上（根据 SwapLogoRight 设置），展开时指向下方
        if (_mainSwap is not null)
        {
            var rotate = ContainerAnimation.EnsureRotate(_mainSwap, 180d);
            var targetAngle = value ? SwapLogoRight ? 270d : 0d : 180d;
            if (ContainerAnimation.CanAnimate(this))
            {
                AnimationService.Run(
                    new RotateTransformAnimation(
                        _mainSwap,
                        targetAngle - rotate.Angle,
                        250,
                        ease: new FluentEaseOut(EasePower.ExtraStrong)),
                    SwapAnimationKey,
                    true);
            }
            else
            {
                AnimationService.CancelAnimationByName(SwapAnimationKey);
                rotate.Angle = targetAngle;
            }
        }
    }

    private void _OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (HasMouseAnimation)
            AnimateShadow(true);

        AnimateTitleColors(true);
    }

    private void _OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isSwapMouseDown = false;
        if (HasMouseAnimation)
            AnimateShadow(false);

        AnimateTitleColors(true);
    }

    private void _OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var pos = e.GetPosition(this).Y;
        if (!IsSwapped && !_IsInHeader(pos))
            return;

        _isCustomMouseDown = true;
        if (!IsSwapped && (SwapControl is null || !_IsInHeader(pos)))
        {
            _isCustomMouseDown = false;
            return;
        }

        _isSwapMouseDown = true;
    }

    private void _OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isCustomMouseDown)
            return;

        _isCustomMouseDown = false;
        // 点击标题区域后先派发页面配置的自定义事件
        CustomEventService.Raise(this);

        if (!_isSwapMouseDown)
            return;

        _isSwapMouseDown = false;

        var pos = e.GetPosition(this).Y;
        if (!IsSwapped && (SwapControl is null || !_IsInHeader(pos)))
            return; // 检测点击位置；或已经不在可视树上的误判

        var args = new RouteEventArgs(true);
        PreviewSwap?.Invoke(this, args);
        if (args.handled)
        {
            _isSwapMouseDown = false;
            return;
        }

        ExecutePreviewSwapCommand();

        IsSwapped = !IsSwapped;
        LogWrapper.Debug(
            $"[Control] {(IsSwapped ? "折叠卡片" : "展开卡片")}{(Title is null ? "" : $"：{Title}")}");
        Swap?.Invoke(this, args);
        ExecuteCommand();
    }

    private bool _IsInHeader(double positionY)
    {
        return positionY <= (IsSwapped ? SwapedHeight : SwapedHeight - 6d)
            && !(positionY == 0d && !IsPointerOver);
    }

    private bool _isCommandRunning;

    private void ExecuteCommand()
    {
        var command = Command;
        if (_isCommandRunning || command is null || !command.CanExecute(CommandParameter))
            return;
        _isCommandRunning = true;
        try
        {
            command.Execute(CommandParameter);
        }
        finally
        {
            _isCommandRunning = false;
        }
    }

    private bool _isPreviewSwapCommandRunning;

    private void ExecutePreviewSwapCommand()
    {
        var command = PreviewSwapCommand;
        if (_isPreviewSwapCommandRunning
            || command is null
            || !command.CanExecute(CommandParameter))
            return;
        _isPreviewSwapCommandRunning = true;
        try
        {
            command.Execute(CommandParameter);
        }
        finally
        {
            _isPreviewSwapCommandRunning = false;
        }
    }

    #endregion

    #region 收起与移除

    private bool _isDisposing;

    /// <summary>
    ///     收起卡片并移除或隐藏：缩放、淡出与高度收拢同时进行，结束后执行回调。
    ///     页面在删除卡片时调用。重复调用会跳过动画，直接收尾。
    /// </summary>
    /// <param name="removeFromChildren">为 <see langword="true" /> 时从父面板移除，否则仅隐藏。</param>
    /// <param name="callBack">动画结束后在 UI 线程执行的回调，参数为本卡片。</param>
    public void AniDispose(bool removeFromChildren, Action<MyCard>? callBack = null)
    {
        if (_isDisposing) return;
        _isDisposing = true;

        AnimationService.CancelAnimationByName(HeightAnimationKey);
        _isHeightAnimating = false;
        ApplyLayoutClip();

        AnimationService.DisposeControl(
            this,
            removeFromChildren,
            _ => callBack?.Invoke(this),
            0.92d,
            true,
            DisposeAnimationKey);
    }

    private string DisposeAnimationKey => $"MyCard Dispose {uuid}";

    #endregion

    #region 主题

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += _OnActualThemeVariantChanged;
        _ApplyThemeBrushes();
        Init();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= _OnActualThemeVariantChanged;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TitleProperty)
            _ApplyTitle(change.NewValue as string);
        else if (change.Property == CornerRadiusProperty
                 && change.NewValue is CornerRadius cornerRadius)
            _ApplyCornerRadius(cornerRadius);
        else if (change.Property == BackgroundBrushProperty)
            _ApplyBackgroundBrush(change.NewValue as IBrush);
        else if (change.Property == IsSwappedProperty && change.NewValue is bool isSwapped)
            _ApplyIsSwapped(isSwapped);
        else if (change.Property == HeightProperty)
            ApplyLayoutClip();
    }

    private void _OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        _ApplyThemeBrushes(true);
    }

    private void _ApplyThemeBrushes(bool animate = false)
    {
        AnimateTitleColors(animate);
        AnimateBackground(animate);
    }

    /// <summary>
    ///     标题与折叠箭头的颜色：悬停时为强调色，其余情况跟随主题文本色。
    ///     悬停与主题切换调用同一段逻辑，后发起的过渡会接管正在进行的过渡。
    /// </summary>
    private void AnimateTitleColors(bool animate)
    {
        var hover = IsPointerOver && HasMouseAnimation;
        var brush = ContainerTheme.Brush(
            this,
            hover ? "ColorBrush2" : "ColorBrush1",
            hover ? Brushes.DodgerBlue : Brushes.Black);

        AnimateBrush(
            _mainTextBlock,
            TextBlock.ForegroundProperty,
            brush,
            TitleColorTime,
            animate,
            TitleColorKey);
        AnimateBrush(
            _mainSwap,
            Shape.FillProperty,
            brush,
            TitleColorTime,
            animate,
            ArrowColorKey);
    }

    /// <summary>卡片底色：主题切换与 BackgroundBrush 变更时做颜色过渡。</summary>
    private void AnimateBackground(bool animate)
    {
        AnimateBrush(
            _mainBorder,
            Border.BackgroundProperty,
            BackgroundBrush ?? _ResolveCardBrush(),
            ThemeColorTime,
            animate,
            BackgroundColorKey);
    }

    /// <summary>
    ///     把画刷过渡到目标值；动画关闭、控件不可动画或画刷非纯色时直接赋终值。
    ///     只影响颜色属性，不触碰高度与缩放，避免与卡片尺寸动画互相覆盖。
    /// </summary>
    private void AnimateBrush(
        Control? target,
        AvaloniaProperty property,
        IBrush brush,
        int time,
        bool animate,
        string name)
    {
        if (target is null)
            return;

        var current = target.GetValue(property);
        if (!animate
            || !ContainerAnimation.CanAnimate(target)
            || brush is not ISolidColorBrush targetBrush
            || current is not ISolidColorBrush currentBrush)
        {
            AnimationService.CancelAnimationByName(name);
            target.SetValue(property, brush);
            return;
        }

        var delta = new NColor(targetBrush) - ContainerAnimation.ColorOf(currentBrush);
        if (Math.Abs(delta.A) < 0.01d
            && Math.Abs(delta.R) < 0.01d
            && Math.Abs(delta.G) < 0.01d
            && Math.Abs(delta.B) < 0.01d)
        {
            // 已经到位：停掉可能仍在进行的反向过渡，避免颜色继续走向旧目标
            AnimationService.CancelAnimationByName(name);
            return;
        }

        AnimationService.Run(new ColorAnimation(target, property, delta, time), name);
    }

    private IBrush _ResolveTextBrush()
    {
        if (this.TryFindResource("ColorBrush1", out var resource) && resource is IBrush brush)
            return brush;

        return Brushes.Black;
    }

    private IBrush _ResolveCardBrush()
    {
        if (this.TryFindResource("ColorBrushTransparentBackground", out var resource)
            && resource is IBrush brush)
            return brush;

        return Brushes.Transparent;
    }

    private void AnimateShadow(bool hover)
    {
        var colorKey = hover ? "ColorObject4" : "ColorObject1";
        var targetOpacity = hover ? DropShadowHoverOpacity : DropShadowIdleOpacity;

        if (!ContainerAnimation.CanAnimate(this) || !this.TryFindResource(colorKey, out _))
        {
            AnimationService.CancelAnimationByName(MouseAnimationKey);
            ContainerTheme.BindDynamic(MainChrome, MyDropShadow.ColorProperty, colorKey);
            MainChrome.Opacity = targetOpacity;
            return;
        }

        AnimationService.Run(
            new List<IAnimation>
            {
                new ColorAnimation(
                    MainChrome,
                    MyDropShadow.ColorProperty,
                    colorKey,
                    ShadowColorTime),
                new OpacityAnimation(
                    MainChrome,
                    targetOpacity - MainChrome.Opacity,
                    ShadowColorTime)
            },
            MouseAnimationKey);
    }

    private string HeightAnimationKey => $"MyCard Height {uuid}";

    private string SwapAnimationKey => $"MyCard Swap {uuid}";

    private string MouseAnimationKey => $"MyCard Mouse {uuid}";

    private string TitleColorKey => $"MyCard Title Color {uuid}";

    private string ArrowColorKey => $"MyCard Arrow Color {uuid}";

    private string BackgroundColorKey => $"MyCard Background Color {uuid}";

    #endregion
}