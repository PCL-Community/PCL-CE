using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace PCL;

[ContentProperty("Inlines")]
public partial class MyExtraTextButton
{
    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e); // 自定义事件

    // 自定义事件
    // 务必放在 IsMouseDown 更新之后
    private const int AnimationColorIn = 120;
    private const int AnimationColorOut = 150;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyExtraTextButton), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyExtraTextButton)sender).LabText.Text = (string)e.NewValue;
        }));

    private string _Logo = "";
    private double _LogoScale = 1d;

    // 动画
    private bool _Show;

    // 鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    private bool IsLeftMouseHeld;

    // 自定义属性
    public int Uuid = ModBase.GetUuid();

    public MyExtraTextButton()
    {
        InitializeComponent();

        Loaded += (_, _) => RefreshColor();
        IsEnabledChanged += (_, _) => RefreshColor();
        PanClick.MouseLeftButtonDown += Button_LeftMouseDown;
        PanClick.MouseLeftButtonUp += Button_LeftMouseUp;
        PanClick.MouseLeave += Button_MouseLeave;
        PanClick.MouseRightButtonUp += Button_RightMouseUp;
        PanClick.MouseEnter += (sender, e) => RefreshColor();
    }

    public string Logo
    {
        get => _Logo;
        set
        {
            if ((value ?? "") == (_Logo ?? ""))
                return;
            _Logo = value;
            Path.Data = (Geometry)new GeometryConverter().ConvertFromString(value);
        }
    }

    public double LogoScale
    {
        get => _LogoScale;
        set
        {
            _LogoScale = value;
            if (Path is not null)
                Path.RenderTransform = new ScaleTransform { ScaleX = LogoScale, ScaleY = LogoScale };
        }
    }

    // 显示文本
    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set
        {
            if (value == null) return;
            SetValue(TextProperty, value);
        }
    }

    public bool Show
    {
        get => _Show;
        set
        {
            if (_Show == value)
                return;
            _Show = value;
            ModBase.RunInUi(() =>
            {
                if (value)
                {
                    // 有了
                    Opacity = 0d;
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaOpacity(this, 1d - Opacity, 80, 50),
                            ModAnimation.AaScaleTransform(this, 0.15d - ((ScaleTransform)RenderTransform).ScaleX, 400,
                                50, new ModAnimation.AniEaseOutBack()),
                            ModAnimation.AaScaleTransform(this, 0.85d, 160, 50, new ModAnimation.AniEaseOutFluent())
                        }, "MyExtraTextButton MainScale " + Uuid);
                }
                else
                {
                    // 没了
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaOpacity(this, -Opacity, 50, 50),
                            ModAnimation.AaScaleTransform(this, -((ScaleTransform)RenderTransform).ScaleX, 100,
                                Ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak))
                        }, "MyExtraTextButton MainScale " + Uuid);
                }

                IsHitTestVisible = value; // 防止缩放动画中依然可以点进去
            });
        }
    }

    // 声明
    public event ClickEventHandler? Click;

    // 触发点击事件
    private void Button_LeftMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsLeftMouseHeld) return;
        ModBase.Log("[Control] 按下附加图标按钮：" + Text);
        Click?.Invoke(sender, e);
        e.Handled = true;
        ModMain.RaiseCustomEvent(this);
        Button_LeftMouseUp();
    }

    private void Button_LeftMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsLeftMouseHeld)
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaScaleTransform(PanScale, 0.85d - ((ScaleTransform)PanScale.RenderTransform).ScaleX,
                        800, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                    ModAnimation.AaScaleTransform(PanScale, -0.05d, 60, Ease: new ModAnimation.AniEaseOutFluent())
                }, "MyExtraTextButton Scale " + Uuid);
        IsLeftMouseHeld = true;
        Focus();
    }

    private void Button_LeftMouseUp()
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanScale, 1d - ((ScaleTransform)PanScale.RenderTransform).ScaleX, 300,
                    Ease: new ModAnimation.AniEaseOutBack())
            }, "MyExtraTextButton Scale " + Uuid);
        IsLeftMouseHeld = false;
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    private void Button_RightMouseUp(object sender, MouseEventArgs e)
    {
        if (!IsLeftMouseHeld)
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaScaleTransform(PanScale, 1d - ((ScaleTransform)PanScale.RenderTransform).ScaleX, 300,
                        Ease: new ModAnimation.AniEaseOutBack())
                }, "MyExtraTextButton Scale " + Uuid);
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    private void Button_MouseLeave(object sender, MouseEventArgs e)
    {
        IsLeftMouseHeld = false;
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanScale, 1d - ((ScaleTransform)PanScale.RenderTransform).ScaleX, 500,
                    Ease: new ModAnimation.AniEaseOutFluent())
            }, "MyExtraTextButton Scale " + Uuid);
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    public void RefreshColor()
    {
        try
        {
            if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
            {
                if (!IsEnabled)
                    // 禁用
                    ModAnimation.AniStart(
                        ModAnimation.AaColor(PanColor, BackgroundProperty, "ColorBrushGray4", AnimationColorIn),
                        "MyExtraTextButton Color " + Uuid);
                else if (IsMouseOver)
                    // 指向
                    ModAnimation.AniStart(
                        ModAnimation.AaColor(PanColor, BackgroundProperty, "ColorBrush4", AnimationColorIn),
                        "MyExtraTextButton Color " + Uuid);
                else
                    // 普通
                    ModAnimation.AniStart(
                        ModAnimation.AaColor(PanColor, BackgroundProperty, "ColorBrush3", AnimationColorOut),
                        "MyExtraTextButton Color " + Uuid);
            }

            else
            {
                ModAnimation.AniStop("MyExtraTextButton Color " + Uuid);
                if (!IsEnabled)
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrushGray4");
                else if (IsMouseOver)
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrush4");
                else
                    PanColor.SetResourceReference(BackgroundProperty, "ColorBrush3");
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新附加图标按钮颜色出错");
        }
    }
}