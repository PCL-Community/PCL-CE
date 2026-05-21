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
        DependencyProperty.Register("Title", typeof(string), typeof(MyCard), new PropertyMetadata(""));

    private readonly BlurBorder MainBorder;

    // 控件
    private readonly Grid MainGrid;
    private Path _MainSwap;
    private TextBlock _MainTextBlock;
    private bool IsLoad;

    // UI 建立
    public MyCard() : base(BlurBorder.BackgroundProperty)
    {
        MainChrome = new MyDropShadow
        {
            Margin = new Thickness(-3, -3, -3, -3 - ModBase.GetWPFSize(1d)), ShadowRadius = 3d,
            Opacity = DropShadowIdleOpacity, CornerRadius = new CornerRadius(5d)
        };
        MainChrome.SetResourceReference(MyDropShadow.ColorProperty, "ColorObject1");
        Children.Insert(0, MainChrome);
        MainBorder = new BlurBorder { CornerRadius = new CornerRadius(5d), IsHitTestVisible = false };
        Children.Insert(1, MainBorder);
        MainGrid = new Grid();
        Children.Add(MainGrid);
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
        get => MainBorder.Child;
        set => MainBorder.Child = value;
    }

    public TextBlock MainTextBlock
    {
        get
        {
            Init(); // 当父级触发 Loaded 时，本卡片可能尚未触发 Loaded（该事件从父级向子级调用），因此这会是 null。手动触发以确保控件已加载。
            return _MainTextBlock;
        }
        set => _MainTextBlock = value;
    }

    public Path MainSwap
    {
        get
        {
            Init();
            return _MainSwap;
        }
        set => _MainSwap = value;
    }

    // 属性
    public InlineCollection Inlines => MainTextBlock.Inlines;

    public CornerRadius CornerRadius
    {
        get => MainChrome.CornerRadius;
        set
        {
            MainChrome.CornerRadius = value;
            MainBorder.CornerRadius = value;
        }
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set
        {
            SetValue(TitleProperty, value);
            if (_MainTextBlock is not null)
                MainTextBlock.Text = value;
        }
    }

    protected override SolidColorBrush AnimatableBrush
    {
        get => (SolidColorBrush)MainBorder.Background;
        set => MainBorder.Background = value;
    }

    protected override FrameworkElement AnimatableElement => MainBorder;
    public bool HasMouseAnimation { get; set; } = true;

    private void Init()
    {
        if (IsLoad)
            return;
        IsLoad = true;
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
            MainGrid.Children.Add(MainTextBlock);
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
            MainGrid.Children.Add(MainSwap);
        }

        // 改变默认的折叠
        if (IsSwapped && SwapControl is not null)
        {
            MainSwap.RenderTransform = new RotateTransform(SwapLogoRight ? 270 : 0);
            SwapControl.Visibility = Visibility.Collapsed;
            // 取消由于高度变化被迫触发的高度动画
            var RawUseAnimation = UseAnimation;
            UseAnimation = false;
            Height = SwapedHeight;
            ModAnimation.AniStop("MyCard Height " + Uuid);
            IsHeightAnimating = false;
            ModBase.RunInUi(() => UseAnimation = RawUseAnimation, true);
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
            ModBase.Log(ex, "[MyCard] InstallMethod 调用失败");
        }

        stack.Children.Add(new FrameworkElement { Height = 18d }); // 下边距，同时适应折叠
        stack.Tag = null;
    }

    private void MyCard_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!HasMouseAnimation)
            return;
        var AniList = new List<ModAnimation.AniData>();
        if (!(MainTextBlock == null))
            AniList.Add(ModAnimation.AaColor(MainTextBlock, TextBlock.ForegroundProperty, "ColorBrush2", 90));
        if (!(MainSwap == null))
            AniList.Add(ModAnimation.AaColor(MainSwap, Shape.FillProperty, "ColorBrush2", 90));
        AniList.AddRange(new[]
        {
            ModAnimation.AaColor(MainChrome, MyDropShadow.ColorProperty, "ColorObject4", 90),
            ModAnimation.AaOpacity(MainChrome, DropShadowHoverOpacity - MainChrome.Opacity, 90)
        });
        if (!IsAnimating)
            ModAnimation.AniStart(AniList, "MyCard Mouse " + Uuid);
    }

    private void MyCard_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!HasMouseAnimation)
            return;
        var AniList = new List<ModAnimation.AniData>();
        if (!(MainTextBlock == null))
            AniList.Add(ModAnimation.AaColor(MainTextBlock, TextBlock.ForegroundProperty, "ColorBrush1", 90));
        if (!(MainSwap == null))
            AniList.Add(ModAnimation.AaColor(MainSwap, Shape.FillProperty, "ColorBrush1", 90));
        AniList.AddRange(new[]
        {
            ModAnimation.AaColor(MainChrome, MyDropShadow.ColorProperty, "ColorObject1", 90),
            ModAnimation.AaOpacity(MainChrome, DropShadowIdleOpacity - MainChrome.Opacity, 90)
        });
        if (!IsAnimating)
            ModAnimation.AniStart(AniList, "MyCard Mouse " + Uuid);
    }

    #region 高度改变动画

    /// <summary>
    ///     是否启用高度改变动画。
    /// </summary>
    public bool UseAnimation { get; set; } = true;

    private bool IsHeightAnimating;
    private double ActualUsedHeight; // 回滚实际高度（例如 NaN）

    private void MySizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!UseAnimation)
            return;
        var DeltaHeight = (IsSwapped ? SwapedHeight : e.NewSize.Height) - e.PreviousSize.Height;
        // 卡片的进入时动画已被页面通用切换动画替代
        if (e.PreviousSize.Height == 0d || IsHeightAnimating || Math.Abs(DeltaHeight) < 1d || ActualHeight == 0d)
            return;
        StartHeightAnimation(DeltaHeight, e.PreviousSize.Height, false);
    }

    /// <summary>
    ///     启动卡片高度变化的动画效果
    ///     根据变化距离的大小采用不同的动画策略：短距离使用简单缓动，长距离使用分段动画
    /// </summary>
    /// <param name="Delta">高度变化量</param>
    /// <param name="PreviousHeight">之前的高度</param>
    /// <param name="IsLoadAnimation">是否为加载动画</param>
    private void StartHeightAnimation(double Delta, double PreviousHeight, bool IsLoadAnimation)
    {
        if (IsHeightAnimating || ModMain.FrmMain is null)
            return; // 避免 XAML 设计器出错

        var AnimList = new List<ModAnimation.AniData>();
        var AbsDelta = Math.Abs(Delta);

        if (AbsDelta <= 800d)
        {
            // 短距离，直接使用 150ms 的缓动动画
            AnimList.Add(ModAnimation.AaHeight(this, Delta, 150,
                Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)));
        }
        else
        {
            var EaseLength = default(int);
            int EaseTime;
            int InitSpeed; // 到达缓动区前的初速度
            if (Delta < 0d && AbsDelta - EaseLength > 5000d * 0.1d)
            {
                // 收回距离过长 (>0.1s)，强制以 100ms 完成匀速段，然后让减速段更长
                EaseLength = 200;
                EaseTime = 150;
                InitSpeed = (int)Math.Round((AbsDelta - EaseLength) / 0.1d);
            }
            else if (Delta > 0d && AbsDelta - EaseLength > 5000d * 0.6d)
            {
                // 展开距离过长 (>0.6s)，以 5000 速度展示 300ms 匀速段，剩下的距离全部归入减速段
                InitSpeed = 5000;
                EaseLength = (int)Math.Round(AbsDelta - InitSpeed * 0.3d);
                EaseTime = 400;
            }
            else
            {
                // 中程，匀速地快速展开（或收回）
                EaseLength = 150;
                EaseTime = 200;
                InitSpeed = 4000;
            }

            // 匀速段
            AnimList.Add(ModAnimation.AaHeight(this, (AbsDelta - EaseLength) * Math.Sign(Delta),
                (int)Math.Round((AbsDelta - EaseLength) / InitSpeed * 1000d)));
            // 减速段
            AnimList.Add(ModAnimation.AaHeight(this, EaseLength * Math.Sign(Delta), EaseTime,
                Ease: new ModAnimation.AniEaseOutFluentWithInitial(InitSpeed, EaseTime / 1000d, EaseLength),
                After: true));
        }

        AnimList.Add(ModAnimation.AaCode(() =>
        {
            IsHeightAnimating = false;
            Height = ActualUsedHeight;
            if (IsSwapped && SwapControl is not null)
                SwapControl.Visibility = Visibility.Collapsed;
        }, After: true));
        ModAnimation.AniStart(AnimList, "MyCard Height " + Uuid);
        IsHeightAnimating = true;
        ActualUsedHeight = IsSwapped ? SwapedHeight : Height;
        Height = PreviousHeight;
    }

    /// <summary>
    ///     通知 MyCard，控件内容已改变，需要中断动画并瞬间更新高度。
    /// </summary>
    public void TriggerForceResize()
    {
        Height = IsSwapped ? SwapedHeight : double.NaN;
        ModAnimation.AniStop("MyCard Height " + Uuid);
        IsHeightAnimating = false;
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
        get => _IsSwapped;
        set
        {
            if (_IsSwapped == value)
                return;
            _IsSwapped = value;
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
            ModAnimation.AniStart(
                ModAnimation.AaRotateTransform(MainSwap,
                    (_IsSwapped ? SwapLogoRight ? 270 : 0 : 180) - ((RotateTransform)MainSwap.RenderTransform).Angle,
                    250, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                "MyCard Swap " + Uuid, true);
        }
    }

    private bool _IsSwapped;

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
    private bool IsSwapMouseDown = false; //用于触发卡片展开/折叠的 MouseDown
    private bool IsCustomMouseDown = false; //用于触发自定义事件的 MouseDown
    public event PreviewSwapEventHandler? PreviewSwap;

    public delegate void PreviewSwapEventHandler(object sender, ModBase.RouteEventArgs e);

    public event SwapEventHandler? Swap;

    public delegate void SwapEventHandler(object sender, ModBase.RouteEventArgs e);

    public const int SwapedHeight = 40;

    private void MyCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        double Pos = Mouse.GetPosition(this).Y;
        if (!IsSwapped && (Pos > (IsSwapped ? SwapedHeight : SwapedHeight - 6) || (Pos == 0 && !IsMouseDirectlyOver)))
            return;
        IsCustomMouseDown = true;
        if (!IsSwapped && (SwapControl == null || Pos > (IsSwapped ? SwapedHeight : SwapedHeight - 6) || (Pos == 0 && !IsMouseDirectlyOver)))
            return;
        IsSwapMouseDown = true;
    }

    private void MyCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsCustomMouseDown) return;
        IsCustomMouseDown = false;
        ModMain.RaiseCustomEvent(this);

        if (!IsSwapMouseDown) return;
        IsSwapMouseDown = false;

        double Pos = Mouse.GetPosition(this).Y;
        if (!IsSwapped && (SwapControl == null || Pos > (IsSwapped ? SwapedHeight : SwapedHeight - 6) || (Pos == 0 && !IsMouseDirectlyOver)))
            return; // 检测点击位置；或已经不在可视树上的误判

        var e2 = new ModBase.RouteEventArgs(true);
        PreviewSwap?.Invoke(this, e2);
        if (e2.Handled)
        {
            IsSwapMouseDown = false;
            return;
        }

        IsSwapped = !IsSwapped;
        ModBase.Log("[Control] " + (IsSwapped ? "折叠卡片" : "展开卡片") + (Title == null ? "" : "：" + Title));
        Swap?.Invoke(this, e2);
    }

    private void MyCard_MouseLeave_Swap(object sender, MouseEventArgs e)
    {
        IsSwapMouseDown = false;
    }

    #endregion
}

public static partial class ModAnimation
{
    public static void AniDispose(MyCard Control, bool RemoveFromChildren, ParameterizedThreadStart CallBack = null)
    {
        if (Control.IsHitTestVisible)
        {
            Control.IsHitTestVisible = false;
            AniStart(new[]
            {
                AaScaleTransform(Control, -0.08d, 200, Ease: new AniEaseInFluent()),
                AaOpacity(Control, -1, 200, Ease: new AniEaseOutFluent()),
                AaHeight(Control, -Control.ActualHeight, 150, 100, new AniEaseOutFluent()),
                AaCode(() =>
                {
                    if (RemoveFromChildren)
                    {
                        if (Control.Parent is null)
                            return;
                        ((Panel)Control.Parent).Children.Remove(Control);
                    }
                    else
                    {
                        Control.Visibility = Visibility.Collapsed;
                    }

                    if (CallBack is not null)
                        CallBack(Control);
                }, After: true)
            }, "MyCard Dispose " + Control.Uuid);
        }
        else
        {
            if (RemoveFromChildren)
            {
                if (Control.Parent is null)
                    return;
                ((Panel)Control.Parent).Children.Remove(Control);
            }
            else
            {
                Control.Visibility = Visibility.Collapsed;
            }

            if (CallBack is not null)
                CallBack(Control);
        }
    }
}