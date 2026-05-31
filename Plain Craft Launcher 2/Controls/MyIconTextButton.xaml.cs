using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCL;

[ContentProperty("Inlines")]
public partial class MyIconTextButton
{
    public delegate void ChangeEventHandler(object sender, bool raiseByMouse);

    public delegate void CheckEventHandler(object sender, bool raiseByMouse);

    public delegate void ClickEventHandler(object sender, ModBase.RouteEventArgs e);

    public enum ColorState
    {
        Black,
        Highlight
    }

    // 动画

    private const int animationTimeOfMouseIn = 100; // 鼠标指向动画长度
    private const int animationTimeOfMouseOut = 150; // 鼠标移出动画长度

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyIconTextButton), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyIconTextButton)sender).LabText.Text = (string)e.NewValue;
        }));

    public static readonly DependencyProperty ColorTypeProperty = DependencyProperty.Register("ColorType",
        typeof(ColorState), typeof(MyIconTextButton), new PropertyMetadata(ColorState.Black));

    private double _LogoScale = 1d;
    private bool isMouseDown;

    // 基础

    public int Uuid = ModBase.GetUuid();

    public MyIconTextButton()
    {
        InitializeComponent();

        MouseLeftButtonUp += (_, _) => MyIconTextButton_MouseUp();
        MouseLeftButtonDown += (_, _) => MyIconTextButton_MouseDown();
        MouseLeave += (_, _) => MyIconTextButton_MouseLeave();
        MouseEnter += RefreshColor;
        Loaded += RefreshColor;
        IsEnabledChanged += (_, _) => RefreshColor();
    }

    // 自定义属性

    public string Logo
    {
        get => ShapeLogo.Data.ToString();
        set
        {
            if (ShapeLogo is null) return;
            ShapeLogo.Data = (Geometry)new GeometryConverter().ConvertFromString(value)!;
        }
    }

    public double LogoScale
    {
        get => _LogoScale;
        set
        {
            _LogoScale = value;
            if (ShapeLogo is not null)
                ShapeLogo.RenderTransform = new ScaleTransform { ScaleX = LogoScale, ScaleY = LogoScale };
        }
    }

    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    } // 内容

    public ColorState ColorType
    {
        get => (ColorState)GetValue(ColorTypeProperty);
        set
        {
            if (ColorType == value)
                return;
            SetValue(ColorTypeProperty, value);
            RefreshColor();
        }
    } // 颜色类别

    public event CheckEventHandler? Check;
    public event ChangeEventHandler? Change;

    private string CheckedAnimationKey => "MyIconTextButton Checked " + Uuid;
    private string ColorAnimationKey => "MyIconTextButton Color " + Uuid;

    // 点击事件

    public event ClickEventHandler? Click;

    private string GetDefaultForegroundResourceKey()
    {
        return ColorType == ColorState.Highlight ? "ColorBrush3" : "ColorBrush1";
    }

    private void StartForegroundAnimation(string resourceKey, int duration)
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaColor(ShapeLogo, Shape.FillProperty, resourceKey, duration),
                ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty, resourceKey, duration)
            }, CheckedAnimationKey);
    }

    private void StartBackgroundAnimation(string resourceKey, int duration)
    {
        ModAnimation.AniStart(ModAnimation.AaColor(this, BackgroundProperty, resourceKey, duration), ColorAnimationKey);
    }

    private void StartBackgroundAnimation(ModBase.MyColor delta, int duration)
    {
        ModAnimation.AniStart(ModAnimation.AaColor(this, BackgroundProperty, delta, duration), ColorAnimationKey);
    }

    private void MyIconTextButton_MouseUp()
    {
        if (!isMouseDown)
            return;
        ModBase.Log("[Control] 按下带图标按钮：" + Text);
        isMouseDown = false;
        Click?.Invoke(this, new ModBase.RouteEventArgs(true));
        ModMain.RaiseCustomEvent(this);
        RefreshColor();
    }

    private void MyIconTextButton_MouseDown()
    {
        isMouseDown = true;
        RefreshColor();
    }

    private void MyIconTextButton_MouseLeave()
    {
        isMouseDown = false;
        RefreshColor();
    }

    private void RefreshColor(object? obj = null, object? e = null)
    {
        try
        {
            if (ControlVisualHelpers.ShouldAnimate(this, e)) // 防止默认属性变更触发动画，若强制不执行动画，则 e 为 False
            {
                if (isMouseDown)
                {
                    StartBackgroundAnimation("ColorBrush6", 70);
                }
                else if (IsMouseOver)
                {
                    StartForegroundAnimation("ColorBrush3", animationTimeOfMouseIn);
                    StartBackgroundAnimation("ColorBrushBg1", animationTimeOfMouseIn);
                }
                else if (IsEnabled)
                {
                    StartForegroundAnimation(GetDefaultForegroundResourceKey(), animationTimeOfMouseOut);
                    StartBackgroundAnimation(ThemeManager.colorSemiTransparent - Background, animationTimeOfMouseOut);
                }
                else
                {
                    StartForegroundAnimation("ColorBrushGray5", 100);
                    StartBackgroundAnimation(ThemeManager.colorSemiTransparent - Background, animationTimeOfMouseOut);
                }
            }

            else
            {
                // 不使用动画
                ModAnimation.AniStop(CheckedAnimationKey);
                ModAnimation.AniStop(ColorAnimationKey);
                Background = ThemeManager.colorSemiTransparent;
                var foregroundKey = IsEnabled ? GetDefaultForegroundResourceKey() : "ColorBrushGray5";
                ShapeLogo.SetResourceReference(Shape.FillProperty, foregroundKey);
                LabText.SetResourceReference(TextBlock.ForegroundProperty, foregroundKey);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新带图标按钮颜色出错");
        }
    }
}
