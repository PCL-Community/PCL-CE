using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;

namespace PCL;

public partial class MyIconButton
{
    public delegate void ClickEventHandler(object sender, EventArgs e);

    public enum Themes
    {
        Color,
        White,
        Black,
        Red,
        Custom
    }

    // 务必放在 IsMouseDown 更新之后
    private const int AnimationColorIn = 120;
    private const int AnimationColorOut = 150;

    private SolidColorBrush _Foreground = new(Color.FromRgb(128, 128, 128));

    private double _LogoScale = 1d;

    // 自定义属性

    public int Uuid = ModBase.GetUuid();

    public MyIconButton()
    {
        InitializeComponent();

        MouseLeftButtonUp += Button_MouseUp;
        MouseLeftButtonDown += Button_MouseDown;
        MouseLeftButtonUp += (_, _) => Button_MouseUp();
        MouseLeave += (_, _) => Button_MouseLeave();
        MouseEnter += (_, _) => RefreshAnim();
        MouseLeave += (_, _) => RefreshAnim();
        Loaded += (_, _) => RefreshAnim();
    }

    public string Logo
    {
        get => Path.Data.ToString();
        set
        {
            if (Path == null) return;
            Path.Data = (Geometry)new GeometryConverter().ConvertFromString(value);
        }
    }

    public double LogoScale
    {
        get => _LogoScale;
        set
        {
            _LogoScale = value;
            if (!(Path == null))
                Path.RenderTransform = new ScaleTransform { ScaleX = LogoScale, ScaleY = LogoScale };
        }
    }

    public Themes Theme { get; set; } = Themes.Color;

    public SolidColorBrush Foreground
    {
        get => _Foreground;
        set
        {
            _Foreground = value;
            ModAnimation.AniControlEnabled += 1;
            RefreshAnim();
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    // 自定义事件
    public event ClickEventHandler? Click;

    //鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    private bool IsMouseDown = false;
    private void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsMouseDown)
            return;
        ModBase.Log("[Control] 按下图标按钮" + (string.IsNullOrEmpty(Name) ? "" : "：" + Name));
        Click?.Invoke(sender, e);
        e.Handled = true;
        Button_MouseUp();
        ModMain.RaiseCustomEvent(this);
    }

    private void Button_MouseDown(object sender, MouseButtonEventArgs e)
    {
        IsMouseDown = true;
        Focus();
        // 指向
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(PanBack, 0.8d - ((ScaleTransform)PanBack.RenderTransform).ScaleX,
                Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
            "MyIconButton Scale " + Uuid);
    }

    private void Button_MouseUp()
    {
        if (IsMouseDown)
        {
            IsMouseDown = false;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaScaleTransform(PanBack, 1.05d - ((ScaleTransform)PanBack.RenderTransform).ScaleX,
                        250, Ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaScaleTransform(PanBack, -0.05d, 250,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong))
                }, "MyIconButton Scale " + Uuid);
        }

        RefreshAnim(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    private void Button_MouseLeave()
    {
        IsMouseDown = false;
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanBack, 1d - ((ScaleTransform)PanBack.RenderTransform).ScaleX, 250,
                    Ease: new ModAnimation.AniEaseOutFluent())
            }, "MyIconButton Scale " + Uuid);
        RefreshAnim(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    public void RefreshAnim()
    {
        try
        {
            if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
            {
                if (PanBack.Background is null)
                    PanBack.Background = new ModBase.MyColor(0d, 255d, 255d, 255d);
                if (Path.Fill is null)
                    switch (Theme)
                    {
                        case Themes.Red:
                        {
                            Path.Fill = new ModBase.MyColor(160d, 255d, 76d, 76d);
                            break;
                        }
                        case Themes.Black:
                        {
                            if (ModSecret.IsDarkMode)
                                Path.Fill = new ModBase.MyColor(160d, 255d, 255d, 255d);
                            else
                                Path.Fill = new ModBase.MyColor(160d, 0d, 0d, 0d);

                            break;
                        }
                        case Themes.Custom:
                        {
                            Path.Fill = new ModBase.MyColor(160d, Foreground);
                            break;
                        }
                    }

                if (IsMouseOver)
                {
                    // 指向
                    var AnimList = new List<ModAnimation.AniData>();
                    switch (Theme)
                    {
                        case Themes.Color:
                        {
                            AnimList.Add(
                                ModAnimation.AaColor(Path, Shape.FillProperty, "ColorBrush2", AnimationColorIn));
                            break;
                        }
                        case Themes.White:
                        {
                            AnimList.Add(ModAnimation.AaColor(PanBack, BackgroundProperty,
                                new ModBase.MyColor(50d, 255d, 255d, 255d) - PanBack.Background, AnimationColorIn));
                            break;
                        }
                        case Themes.Red:
                        {
                            AnimList.Add(ModAnimation.AaColor(Path, Shape.FillProperty,
                                new ModBase.MyColor(255d, 76d, 76d) - Path.Fill, AnimationColorIn));
                            break;
                        }
                        case Themes.Black:
                        {
                            AnimList.Add(ModAnimation.AaColor(Path, Shape.FillProperty,
                                (ModSecret.IsDarkMode
                                    ? new ModBase.MyColor(230d, 255d, 255d, 255d)
                                    : new ModBase.MyColor(230d, 0d, 0d, 0d)) - Path.Fill, AnimationColorIn));
                            break;
                        }
                        case Themes.Custom:
                        {
                            AnimList.Add(ModAnimation.AaColor(Path, Shape.FillProperty,
                                new ModBase.MyColor(255d, Foreground) - Path.Fill, AnimationColorIn));
                            break;
                        }
                    }

                    ModAnimation.AniStart(AnimList, "MyIconButton Color " + Uuid);
                }
                else
                {
                    // 普通
                    var AnimList = new List<ModAnimation.AniData>();
                    switch (Theme)
                    {
                        case Themes.Color:
                        {
                            AnimList.Add(ModAnimation.AaColor(Path, Shape.FillProperty, "ColorBrush4",
                                AnimationColorOut));
                            PanBack.Background = new ModBase.MyColor(0d, 255d, 255d, 255d);
                            break;
                        }
                        case Themes.White:
                        {
                            AnimList.Add(ModAnimation.AaColor(Path, Shape.FillProperty,
                                new ModBase.MyColor(234d, 242d, 254d), AnimationColorOut));
                            AnimList.Add(ModAnimation.AaColor(PanBack, BackgroundProperty,
                                new ModBase.MyColor(0d, 255d, 255d, 255d) - PanBack.Background, AnimationColorOut));
                            break;
                        }
                        case Themes.Red:
                        {
                            AnimList.Add(ModAnimation.AaColor(Path, Shape.FillProperty,
                                new ModBase.MyColor(160d, 255d, 76d, 76d) - Path.Fill, AnimationColorOut));
                            PanBack.Background = new ModBase.MyColor(0d, 255d, 255d, 255d);
                            break;
                        }
                        case Themes.Black:
                        {
                            AnimList.Add(ModAnimation.AaColor(Path, Shape.FillProperty,
                                (ModSecret.IsDarkMode
                                    ? new ModBase.MyColor(160d, 255d, 255d, 255d)
                                    : new ModBase.MyColor(160d, 0d, 0d, 0d)) - Path.Fill, AnimationColorOut));
                            PanBack.Background = new ModBase.MyColor(0d, 255d, 255d, 255d);
                            break;
                        }
                        case Themes.Custom:
                        {
                            AnimList.Add(ModAnimation.AaColor(Path, Shape.FillProperty,
                                new ModBase.MyColor(160d, Foreground) - Path.Fill, AnimationColorOut));
                            PanBack.Background = new ModBase.MyColor(0d, 255d, 255d, 255d);
                            break;
                        }
                    }

                    ModAnimation.AniStart(AnimList, "MyIconButton Color " + Uuid);
                }
            }

            else
            {
                ModAnimation.AniStop("MyIconButton Color " + Uuid);
                switch (Theme)
                {
                    case Themes.Color:
                    {
                        Path.SetResourceReference(Shape.FillProperty, "ColorBrush5");
                        break;
                    }
                    case Themes.White:
                    {
                        Path.Fill = new ModBase.MyColor(234d, 242d, 254d);
                        break;
                    }
                    case Themes.Red:
                    {
                        Path.Fill = new ModBase.MyColor(160d, 255d, 76d, 76d);
                        break;
                    }
                    case Themes.Black:
                    {
                        if (ModSecret.IsDarkMode)
                            Path.Fill = new ModBase.MyColor(160d, 255d, 255d, 255d);
                        else
                            Path.Fill = new ModBase.MyColor(160d, 0d, 0d, 0d);

                        break;
                    }
                    case Themes.Custom:
                    {
                        Path.Fill = new ModBase.MyColor(160d, Foreground);
                        break;
                    }
                }

                PanBack.Background = new ModBase.MyColor(0d, 255d, 255d, 255d);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新图标按钮动画状态出错");
        }
    }
}

public static partial class ModAnimation
{
    public static void AniDispose(MyIconButton Control, bool RemoveFromChildren,
        ParameterizedThreadStart CallBack = null)
    {
        if (!Control.IsHitTestVisible)
            return;
        Control.IsHitTestVisible = false;
        AniStart(new[]
        {
            AaScaleTransform(Control, -1.5d, 200, Ease: new AniEaseInFluent()),
            AaCode(() =>
            {
                if (RemoveFromChildren)
                    ((Panel)Control.Parent).Children.Remove(Control);
                else
                    Control.Visibility = Visibility.Collapsed;
                if (CallBack is not null)
                    CallBack(Control);
            }, After: true)
        }, "MyIconButton Dispose " + Control.Uuid);
    }
}