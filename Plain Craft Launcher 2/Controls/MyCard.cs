using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.UI.Controls;

namespace PCL;

public class MyCard : AnimatedBackgroundGrid
{
    // 动画
    private const double DropShadowIdleOpacity = 0.07d;
    private const double DropShadowHoverOpacity = 0.4d;

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register("Title", typeof(string), typeof(MyCard), new PropertyMetadata("", OnTitleChanged));

    private readonly BlurBorder _mainBorder;

    // 控件
    private readonly Grid _mainGrid;
    private TextBlock _mainTextBlock;
    private bool _isLoad;

    // UI 建立
    public MyCard() : base(BlurBorder.BackgroundProperty)
    {
        MainChrome = new MyDropShadow
        {
            Margin = new Thickness(-3, -3, -3, -3 - Common.GetWpfSize(1d)), ShadowRadius = 3d,
            Opacity = DropShadowIdleOpacity, CornerRadius = new CornerRadius(5d)
        };
        MainChrome.SetResourceReference(MyDropShadow.ColorProperty, "ColorObject1");
        Children.Insert(0, MainChrome);
        _mainBorder = new BlurBorder
            { CornerRadius = new CornerRadius(5d), IsHitTestVisible = false };
        Children.Insert(1, _mainBorder);
        _mainGrid = new Grid();
        Children.Add(_mainGrid);
        // 设置背景色
        SetResourceReference(BackgroundBrushProperty, "ColorBrushTransparentBackground");
        Loaded += (_, _) => Init();
        MouseEnter += MyCard_MouseEnter;
        MouseLeave += MyCard_MouseLeave;
        SizeChanged += MySizeChanged;
        MouseLeftButtonDown += MyCard_MouseLeftButtonDown;
        MouseLeftButtonUp += MyCard_MouseLeftButtonUp;
        MouseLeave += MyCard_MouseLeave_Swap;
    }

    public MyDropShadow MainChrome { get; }

    public UIElement BorderChild
    {
        get => _mainBorder.Child;
        set => _mainBorder.Child = value;
    }

    public TextBlock MainTextBlock
    {
        get
        {
            Init(); // 当父级触发 Loaded 时，本卡片可能尚未触发 Loaded（该事件从父级向子级调用），因此这会是 null。手动触发以确保控件已加载。
            return _mainTextBlock;
        }
        set => _mainTextBlock = value;
    }

    public Path MainSwap
    {
        get
        {
            Init();
            return field;
        }
        set => field = value;
    }

    // 属性
    public InlineCollection Inlines => MainTextBlock.Inlines;

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(MyCard),
            new PropertyMetadata(new CornerRadius(5d), OnCornerRadiusChanged));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set
        {
            var oldValue = CornerRadius;
            SetValue(CornerRadiusProperty, value);
            if (Equals(oldValue, value))
                ApplyCornerRadius(value);
        }
    }

    private static void OnCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MyCard)d).ApplyCornerRadius((CornerRadius)e.NewValue);

    private void ApplyCornerRadius(CornerRadius value)
    {
        MainChrome.CornerRadius = value;
        _mainBorder.CornerRadius = value;
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set
        {
            var oldValue = Title;
            SetValue(TitleProperty, value);
            if (Equals(oldValue, value))
                ApplyTitle(value);
        }
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MyCard)d).ApplyTitle((string)e.NewValue);

    private void ApplyTitle(string value)
    {
        if (_mainTextBlock is not null)
            MainTextBlock.Text = value;
    }

    protected override SolidColorBrush AnimatableBrush
    {
        get => (SolidColorBrush)_mainBorder.Background;
        set => _mainBorder.Background = value;
    }

    protected override FrameworkElement AnimatableElement => _mainBorder;
    public bool HasMouseAnimation { get; set; } = true;

    private void Init()
    {
        if (_isLoad)
            return;
        _isLoad = true;
        // AddHandler ThemeChanged, AddressOf _BackgroundBrushChanged '已在依赖属性中实现
        // 初次加载限定
        if (MainTextBlock is null)
        {
            MainTextBlock = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(15d, 12d, 0d, 0d), FontWeight = FontWeights.Bold, FontSize = 13d,
                IsHitTestVisible = false
            };
            MainTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");
            MainTextBlock.SetBinding(TextBlock.TextProperty,
                new Binding("Title") { Source = this, Mode = BindingMode.OneWay });
            _mainGrid.Children.Add(MainTextBlock);
        }

        if (CanSwap || SwapControl is not null)
        {
            if (SwapControl is null && Children.Count > 3)
                SwapControl = Children[3];
            MainSwap = new Path
            {
                HorizontalAlignment = HorizontalAlignment.Right, Stretch = Stretch.Uniform, Height = 6d, Width = 10d,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0d, 17d, 16d, 0d),
                Data =
                    (Geometry)new GeometryConverter().ConvertFromString("M2,4 l-2,2 10,10 10,-10 -2,-2 -8,8 -8,-8 z"),
                RenderTransform = new RotateTransform(180d), RenderTransformOrigin = new Point(0.5d, 0.5d)
            };
            MainSwap.SetResourceReference(Shape.FillProperty, "ColorBrush1");
            _mainGrid.Children.Add(MainSwap);
        }

        // 改变默认的折叠
        if (IsSwapped && SwapControl is not null)
        {
            MainSwap.RenderTransform = new RotateTransform(SwapLogoRight ? 270 : 0);
            SwapControl.Visibility = Visibility.Collapsed;
            // 取消由于高度变化被迫触发的高度动画
            var rawUseAnimation = UseAnimation;
            UseAnimation = false;
            Height = SwapedHeight;
            Animations.AniStop("MyCard Height " + uuid);
            _isHeightAnimating = false;
            Common.RunInUi(() => UseAnimation = rawUseAnimation, true);
        }
    }

    public void StackInstall()
    {
        var argstack = (StackPanel)SwapControl;
        StackInstall(ref argstack, InstallMethod);
        SwapControl = argstack;
        TriggerForceResize();
    }

    public static void StackInstall(ref StackPanel stack, Action<StackPanel> installMethod)
    {
        if (stack.Tag is null)
            return;
        try
        {
            installMethod(stack);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[MyCard] InstallMethod 调用失败");
        }

        stack.Children.Add(new FrameworkElement { Height = 18d }); // 下边距，同时适应折叠
        stack.Tag = null;
    }

    private void MyCard_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!HasMouseAnimation)
            return;
        var aniList = new List<Animations.AniData>();
        if (MainTextBlock is not null)
            aniList.Add(Animations.AaColor(MainTextBlock, TextBlock.ForegroundProperty,
                "ColorBrush2", 90));
        if (MainSwap is not null)
            aniList.Add(Animations.AaColor(MainSwap, Shape.FillProperty, "ColorBrush2", 90));
        aniList.AddRange(new[]
        {
            Animations.AaColor(MainChrome, MyDropShadow.ColorProperty, "ColorObject4", 90),
            Animations.AaOpacity(MainChrome, DropShadowHoverOpacity - MainChrome.Opacity, 90)
        });
        if (!IsAnimating)
            Animations.AniStart(aniList, "MyCard Mouse " + uuid);
    }

    private void MyCard_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!HasMouseAnimation)
            return;
        var aniList = new List<Animations.AniData>();
        if (MainTextBlock is not null)
            aniList.Add(Animations.AaColor(MainTextBlock, TextBlock.ForegroundProperty,
                "ColorBrush1", 90));
        if (MainSwap is not null)
            aniList.Add(Animations.AaColor(MainSwap, Shape.FillProperty, "ColorBrush1", 90));
        aniList.AddRange(new[]
        {
            Animations.AaColor(MainChrome, MyDropShadow.ColorProperty, "ColorObject1", 90),
            Animations.AaOpacity(MainChrome, DropShadowIdleOpacity - MainChrome.Opacity, 90)
        });
        if (!IsAnimating)
            Animations.AniStart(aniList, "MyCard Mouse " + uuid);
    }

    #region 高度改变动画

    /// <summary>
    ///     是否启用高度改变动画。
    /// </summary>
    public bool UseAnimation { get; set; } = true;

    private bool _isHeightAnimating;
    private double _actualUsedHeight; // 回滚实际高度（例如 NaN）

    private void MySizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!UseAnimation)
            return;
        var deltaHeight = (IsSwapped ? SwapedHeight : e.NewSize.Height) - e.PreviousSize.Height;
        // 卡片的进入时动画已被页面通用切换动画替代
        if (e.PreviousSize.Height == 0d || _isHeightAnimating || Math.Abs(deltaHeight) < 1d ||
            ActualHeight == 0d)
            return;
        StartHeightAnimation(deltaHeight, e.PreviousSize.Height, false);
    }

    /// <summary>
    ///     启动卡片高度变化的动画效果
    ///     根据变化距离的大小采用不同的动画策略：短距离使用简单缓动，长距离使用分段动画
    /// </summary>
    /// <param name="delta">高度变化量</param>
    /// <param name="previousHeight">之前的高度</param>
    /// <param name="isLoadAnimation">是否为加载动画</param>
    private void StartHeightAnimation(double delta, double previousHeight, bool isLoadAnimation)
    {
        if (_isHeightAnimating || Launcher.frmMain is null)
            return; // 避免 XAML 设计器出错

        var animList = new List<Animations.AniData>();
        var absDelta = Math.Abs(delta);

        if (absDelta <= 800d)
        {
            // 短距离，直接使用 150ms 的缓动动画
            animList.Add(Animations.AaHeight(this, delta, 150,
                ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.ExtraStrong)));
        }
        else
        {
            var easeLength = default(int);
            int easeTime;
            int initSpeed; // 到达缓动区前的初速度
            if (delta < 0d && absDelta - easeLength > 5000d * 0.1d)
            {
                // 收回距离过长 (>0.1s)，强制以 100ms 完成匀速段，然后让减速段更长
                easeLength = 200;
                easeTime = 150;
                initSpeed = (int)Math.Round((absDelta - easeLength) / 0.1d);
            }
            else if (delta > 0d && absDelta - easeLength > 5000d * 0.6d)
            {
                // 展开距离过长 (>0.6s)，以 5000 速度展示 300ms 匀速段，剩下的距离全部归入减速段
                initSpeed = 5000;
                easeLength = (int)Math.Round(absDelta - initSpeed * 0.3d);
                easeTime = 400;
            }
            else
            {
                // 中程，匀速地快速展开（或收回）
                easeLength = 150;
                easeTime = 200;
                initSpeed = 4000;
            }

            // 匀速段
            animList.Add(Animations.AaHeight(this, (absDelta - easeLength) * Math.Sign(delta),
                (int)Math.Round((absDelta - easeLength) / initSpeed * 1000d)));
            // 减速段
            animList.Add(Animations.AaHeight(this, easeLength * Math.Sign(delta), easeTime,
                ease: new Animations.AniEaseOutFluentWithInitial(initSpeed, easeTime / 1000d,
                    easeLength),
                after: true));
        }

        animList.Add(Animations.AaCode(() =>
        {
            _isHeightAnimating = false;
            Height = _actualUsedHeight;
            if (IsSwapped && SwapControl is not null)
                SwapControl.Visibility = Visibility.Collapsed;
        }, after: true));
        Animations.AniStart(animList, "MyCard Height " + uuid);
        _isHeightAnimating = true;
        _actualUsedHeight = IsSwapped ? SwapedHeight : Height;
        Height = previousHeight;
    }

    /// <summary>
    ///     通知 MyCard，控件内容已改变，需要中断动画并瞬间更新高度。
    /// </summary>
    public void TriggerForceResize()
    {
        Height = IsSwapped ? SwapedHeight : double.NaN;
        Animations.AniStop("MyCard Height " + uuid);
        _isHeightAnimating = false;
    }

    #endregion

    #region 折叠

    // 若设置了 CanSwap，或 SwapControl 不为空，则判定为会进行折叠
    // 这是因为不能直接在 XAML 中设置 SwapControl
    public UIElement SwapControl;
    public bool CanSwap { get; set; } = false;

    /// <summary>
    ///     数据转为列表项的转换方法
    /// </summary>
    /// <returns></returns>
    public Action<StackPanel> InstallMethod { get; set; }

    /// <summary>
    ///     是否已被折叠。
    /// </summary>
    public bool IsSwapped
    {
        get => (bool)GetValue(IsSwappedProperty);
        set
        {
            if (IsSwapped == value)
                return;
            SetValue(IsSwappedProperty, value);
        }
    }

    public static readonly DependencyProperty IsSwappedProperty =
        DependencyProperty.Register(nameof(IsSwapped), typeof(bool), typeof(MyCard),
            new PropertyMetadata(false, OnIsSwappedChanged));

    private static void OnIsSwappedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MyCard)d).ApplyIsSwapped((bool)e.NewValue);

    private void ApplyIsSwapped(bool value)
    {
        if (SwapControl is null)
            return;

        // 当卡片展开时，如果SwapControl是StackPanel类型，则执行安装方法
        // 这通常用于动态添加内容到折叠卡片中
        if (!IsSwapped && SwapControl is StackPanel)
        {
            var argstack = (StackPanel)SwapControl;
            StackInstall(ref argstack, InstallMethod);
            SwapControl = argstack;
        }

        // 若尚未加载，会在 Loaded 事件中触发无动画的折叠，不需要在这里进行
        if (!IsLoaded)
            return;

        // 更新控件的可见性和高度
        SwapControl.Visibility = Visibility.Visible;
        TriggerForceResize();

        // 根据折叠状态旋转箭头图标
        // 折叠时箭头指向右侧或向上（根据SwapLogoRight设置），展开时指向下方
        Animations.AniStart(
            Animations.AaRotateTransform(MainSwap,
                (value ? SwapLogoRight ? 270 : 0 : 180) - ((RotateTransform)MainSwap.RenderTransform).Angle,
                250,
                ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.ExtraStrong)),
            "MyCard Swap " + uuid, true);
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

    public bool SwapLogoRight { get; set; } = false;
    private bool _isSwapMouseDown; //用于触发卡片展开/折叠的 MouseDown
    private bool _isCustomMouseDown; //用于触发自定义事件的 MouseDown
    public event PreviewSwapEventHandler? PreviewSwap;

    public delegate void PreviewSwapEventHandler(object sender, Common.RouteEventArgs e);

    public event SwapEventHandler? Swap;

    public delegate void SwapEventHandler(object sender, Common.RouteEventArgs e);

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command),
        typeof(ICommand), typeof(MyCard), new PropertyMetadata(null));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter), typeof(object), typeof(MyCard), new PropertyMetadata(null));

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public static readonly DependencyProperty PreviewSwapCommandProperty = DependencyProperty.Register(
        nameof(PreviewSwapCommand), typeof(ICommand), typeof(MyCard), new PropertyMetadata(null));

    public ICommand? PreviewSwapCommand
    {
        get => (ICommand?)GetValue(PreviewSwapCommandProperty);
        set => SetValue(PreviewSwapCommandProperty, value);
    }

    public const int SwapedHeight = 40;

    private void MyCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        double pos = Mouse.GetPosition(this).Y;
        if (!IsSwapped && (pos > (IsSwapped ? SwapedHeight : SwapedHeight - 6) || (pos == 0 && !IsMouseDirectlyOver)))
            return;
        _isCustomMouseDown = true;
        if (!IsSwapped && (SwapControl is null || pos > (IsSwapped ? SwapedHeight : SwapedHeight - 6) || (pos == 0 && !IsMouseDirectlyOver)))
            return;
        _isSwapMouseDown = true;
    }

    private void MyCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isCustomMouseDown) return;
        _isCustomMouseDown = false;
        Launcher.RaiseCustomEvent(this);

        if (!_isSwapMouseDown) return;
        _isSwapMouseDown = false;

        double pos = Mouse.GetPosition(this).Y;
        if (!IsSwapped && (SwapControl is null || pos > (IsSwapped ? SwapedHeight : SwapedHeight - 6) || (pos == 0 && !IsMouseDirectlyOver)))
            return; // 检测点击位置；或已经不在可视树上的误判

        var e2 = new Common.RouteEventArgs(true);
        PreviewSwap?.Invoke(this, e2);
        if (e2.handled)
        {
            _isSwapMouseDown = false;
            return;
        }
        ExecutePreviewSwapCommand();

        IsSwapped = !IsSwapped;
        Common.Log(
            "[Control] " + (IsSwapped ? "折叠卡片" : "展开卡片") + (Title is null ? "" : "：" + Title));
        Swap?.Invoke(this, e2);
        ExecuteCommand();
    }

    private void MyCard_MouseLeave_Swap(object sender, MouseEventArgs e)
    {
        _isSwapMouseDown = false;
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
        if (_isPreviewSwapCommandRunning || command is null || !command.CanExecute(CommandParameter))
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
}

public static partial class Animations
{
    public static void AniDispose(MyCard control, bool removeFromChildren, ParameterizedThreadStart callBack = null)
    {
        if (control.IsHitTestVisible)
        {
            control.IsHitTestVisible = false;
            AniStart(new[]
            {
                AaScaleTransform(control, -0.08d, 200, ease: new AniEaseInFluent()),
                AaOpacity(control, -1, 200, ease: new AniEaseOutFluent()),
                AaHeight(control, -control.ActualHeight, 150, 100, new AniEaseOutFluent()),
                AaCode(() =>
                {
                    if (removeFromChildren)
                    {
                        if (control.Parent is null)
                            return;
                        ((Panel)control.Parent).Children.Remove(control);
                    }
                    else
                    {
                        control.Visibility = Visibility.Collapsed;
                    }

                    if (callBack is not null)
                        callBack(control);
                }, after: true)
            }, "MyCard Dispose " + control.uuid);
        }
        else
        {
            if (removeFromChildren)
            {
                if (control.Parent is null)
                    return;
                ((Panel)control.Parent).Children.Remove(control);
            }
            else
            {
                control.Visibility = Visibility.Collapsed;
            }

            if (callBack is not null)
                callBack(control);
        }
    }
}