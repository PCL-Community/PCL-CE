using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.UI.Controls;

namespace PCL;

public class MyCard : BackgroundGrid
{
    private const double dropShadowIdleOpacity = 0.07d;
    private const double dropShadowHoverOpacity = 0.4d;

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register("Title", typeof(string), typeof(MyCard), new PropertyMetadata(""));

    private readonly BlurBorder mainBorder;

    // 控件
    private readonly Grid mainGrid;
    private TextBlock _MainTextBlock;
    private bool isLoad;

    // UI 建立
    public MyCard()
    {
        MainChrome = new MyDropShadow
        {
            Margin = new Thickness(-3, -3, -3, -3 - ModBase.GetWPFSize(1d)), ShadowRadius = 3d,
            Opacity = dropShadowIdleOpacity, CornerRadius = new CornerRadius(5d)
        };
        MainChrome.SetResourceReference(MyDropShadow.ColorProperty, "ColorObject1");
        Children.Insert(0, MainChrome);
        mainBorder = new BlurBorder { CornerRadius = new CornerRadius(5d), IsHitTestVisible = false };
        Children.Insert(1, mainBorder);
        mainGrid = new Grid();
        Children.Add(mainGrid);
        // 设置背景色
        SetResourceReference(BackgroundBrushProperty, "ColorBrushTransparentBackground");
        Loaded += (_, _) => Init();
        MouseEnter += MyCard_MouseEnter;
        MouseLeave += MyCard_MouseLeave;
        MouseLeftButtonDown += MyCard_MouseLeftButtonDown;
        MouseLeftButtonUp += MyCard_MouseLeftButtonUp;
        MouseLeave += MyCard_MouseLeave_Swap;
    }

    public MyDropShadow MainChrome { get; }

    public UIElement BorderChild
    {
        get => mainBorder.Child;
        set => mainBorder.Child = value;
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
            return field;
        }
        set => field = value;
    }

    // 属性
    public InlineCollection Inlines => MainTextBlock.Inlines;

    public CornerRadius CornerRadius
    {
        get => MainChrome.CornerRadius;
        set
        {
            MainChrome.CornerRadius = value;
            mainBorder.CornerRadius = value;
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

    protected override SolidColorBrush SurfaceBrush
    {
        get => (SolidColorBrush)mainBorder.Background;
        set => mainBorder.Background = value;
    }

    private void Init()
    {
        if (isLoad)
            return;
        isLoad = true;
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
            mainGrid.Children.Add(MainTextBlock);
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
            mainGrid.Children.Add(MainSwap);
        }

        // 改变默认的折叠
        if (IsSwapped && SwapControl is not null)
        {
            MainSwap.RenderTransform = new RotateTransform(SwapLogoRight ? 270 : 0);
            SwapControl.Visibility = Visibility.Collapsed;
            Height = SwapedHeight;
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
        SetHoverStyle(true);
    }

    private void MyCard_MouseLeave(object sender, MouseEventArgs e)
    {
        SetHoverStyle(false);
    }

    private void SetHoverStyle(bool isHover)
    {
        if (MainTextBlock is not null)
            MainTextBlock.SetResourceReference(TextBlock.ForegroundProperty, isHover ? "ColorBrush2" : "ColorBrush1");
        if (MainSwap is not null)
            MainSwap.SetResourceReference(Shape.FillProperty, isHover ? "ColorBrush2" : "ColorBrush1");
        MainChrome.SetResourceReference(MyDropShadow.ColorProperty, isHover ? "ColorObject4" : "ColorObject1");
        MainChrome.Opacity = isHover ? dropShadowHoverOpacity : dropShadowIdleOpacity;
    }

    #region 高度改变

    /// <summary>
    ///     通知 MyCard，控件内容已改变，需要立即更新高度。
    /// </summary>
    public void TriggerForceResize()
    {
        Height = IsSwapped ? SwapedHeight : double.NaN;
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
        get => field;
        set
        {
            if (field == value)
                return;
            field = value;
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

            // 若尚未加载，会在 Loaded 事件中触发折叠，不需要在这里进行
            if (!IsLoaded)
                return;

            // 更新控件的可见性和高度
            SwapControl.Visibility = Visibility.Visible;
            TriggerForceResize();

            // 根据折叠状态旋转箭头图标
            // 折叠时箭头指向右侧或向上（根据SwapLogoRight设置），展开时指向下方
            ((RotateTransform)MainSwap.RenderTransform).Angle = field ? SwapLogoRight ? 270 : 0 : 180;
        }
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
    private bool isSwapMouseDown = false; //用于触发卡片展开/折叠的 MouseDown
    private bool isCustomMouseDown = false; //用于触发自定义事件的 MouseDown
    public event PreviewSwapEventHandler? PreviewSwap;

    public delegate void PreviewSwapEventHandler(object sender, ModBase.RouteEventArgs e);

    public event SwapEventHandler? Swap;

    public delegate void SwapEventHandler(object sender, ModBase.RouteEventArgs e);

    public const int SwapedHeight = 40;

    private void MyCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        double pos = Mouse.GetPosition(this).Y;
        if (!IsSwapped && (pos > (IsSwapped ? SwapedHeight : SwapedHeight - 6) || (pos == 0 && !IsMouseDirectlyOver)))
            return;
        isCustomMouseDown = true;
        if (!IsSwapped && (SwapControl is null || pos > (IsSwapped ? SwapedHeight : SwapedHeight - 6) || (pos == 0 && !IsMouseDirectlyOver)))
            return;
        isSwapMouseDown = true;
    }

    private void MyCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isCustomMouseDown) return;
        isCustomMouseDown = false;
        ModMain.RaiseCustomEvent(this);

        if (!isSwapMouseDown) return;
        isSwapMouseDown = false;

        double pos = Mouse.GetPosition(this).Y;
        if (!IsSwapped && (SwapControl is null || pos > (IsSwapped ? SwapedHeight : SwapedHeight - 6) || (pos == 0 && !IsMouseDirectlyOver)))
            return; // 检测点击位置；或已经不在可视树上的误判

        var e2 = new ModBase.RouteEventArgs(true);
        PreviewSwap?.Invoke(this, e2);
        if (e2.handled)
        {
            isSwapMouseDown = false;
            return;
        }

        IsSwapped = !IsSwapped;
        ModBase.Log("[Control] " + (IsSwapped ? "折叠卡片" : "展开卡片") + (Title is null ? "" : "：" + Title));
        Swap?.Invoke(this, e2);
    }

    private void MyCard_MouseLeave_Swap(object sender, MouseEventArgs e)
    {
        isSwapMouseDown = false;
    }

    #endregion

    /// <summary>
    ///     立即移除或隐藏卡片。
    /// </summary>
    /// <param name="control">要移除的卡片。</param>
    /// <param name="removeFromChildren">为 true 时从父容器移除，否则仅隐藏。</param>
    /// <param name="callBack">移除或隐藏完成后的回调。</param>
    public static void DisposeCard(MyCard control, bool removeFromChildren, Action<MyCard>? callBack = null)
    {
        if (removeFromChildren)
        {
            if (control.Parent is not null)
                ((Panel)control.Parent).Children.Remove(control);
        }
        else
        {
            control.Visibility = Visibility.Collapsed;
        }

        callBack?.Invoke(control);
    }
}