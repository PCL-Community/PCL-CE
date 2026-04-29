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

    private const int AnimationTimeOfMouseIn = 100; // 鼠标指向动画长度
    private const int AnimationTimeOfMouseOut = 150; // 鼠标移出动画长度

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyIconTextButton), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyIconTextButton)sender).LabText.Text = (string)e.NewValue;
        }));

    public static readonly DependencyProperty ColorTypeProperty = DependencyProperty.Register("ColorType",
        typeof(ColorState), typeof(MyIconTextButton), new PropertyMetadata(ColorState.Black));

    private double _LogoScale = 1d;
    private bool IsMouseDown;

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
            if (ShapeLogo == null) return;
            ShapeLogo.Data = (Geometry)new GeometryConverter().ConvertFromString(value);
        }
    }

    public double LogoScale
    {
        get => _LogoScale;
        set
        {
            _LogoScale = value;
            if (!(ShapeLogo == null))
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

    // 点击事件

    public event ClickEventHandler? Click;

    private void MyIconTextButton_MouseUp()
    {
        if (!IsMouseDown)
            return;
        ModBase.Log("[Control] 按下带图标按钮：" + Text);
        IsMouseDown = false;
        Click?.Invoke(this, new ModBase.RouteEventArgs(true));
        ModMain.RaiseCustomEvent(this);
        RefreshColor();
    }

    private void MyIconTextButton_MouseDown()
    {
        IsMouseDown = true;
        RefreshColor();
    }

    private void MyIconTextButton_MouseLeave()
    {
        IsMouseDown = false;
        RefreshColor();
    }

    private void RefreshColor(object obj = null, object e = null)
    {
        try
        {
            if (IsLoaded && ModAnimation.AniControlEnabled == 0 &&
                !false.Equals(e)) // 防止默认属性变更触发动画，若强制不执行动画，则 e 为 False
            {
                switch (ColorType)
                {
                    case ColorState.Black:
                    {
                        if (IsMouseDown)
                        {
                            // 按下
                            ModAnimation.AniStart(ModAnimation.AaColor(this, BackgroundProperty, "ColorBrush6", 70),
                                "MyIconTextButton Color " + Uuid);
                        }
                        else if (IsMouseOver)
                        {
                            // 指向
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty, "ColorBrush3",
                                        AnimationTimeOfMouseIn),
                                    ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3",
                                        AnimationTimeOfMouseIn)
                                }, "MyIconTextButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty, "ColorBrushBg1", AnimationTimeOfMouseIn),
                                "MyIconTextButton Color " + Uuid);
                        }
                        else if (IsEnabled)
                        {
                            // 正常
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty, "ColorBrush1",
                                        AnimationTimeOfMouseOut),
                                    ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush1",
                                        AnimationTimeOfMouseOut)
                                }, "MyIconTextButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty,
                                    ModSecret.ColorSemiTransparent - Background, AnimationTimeOfMouseOut),
                                "MyIconTextButton Color " + Uuid);
                        }
                        else
                        {
                            // 禁用
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty, "ColorBrushGray5", 100),
                                    ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrushGray5", 100)
                                }, "MyIconTextButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty,
                                    ModSecret.ColorSemiTransparent - Background, AnimationTimeOfMouseOut),
                                "MyIconTextButton Color " + Uuid);
                        }

                        break;
                    }
                    case ColorState.Highlight:
                    {
                        if (IsMouseDown)
                        {
                            // 按下
                            ModAnimation.AniStart(ModAnimation.AaColor(this, BackgroundProperty, "ColorBrush6", 70),
                                "MyIconTextButton Color " + Uuid);
                        }
                        else if (IsMouseOver)
                        {
                            // 指向
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty, "ColorBrush3",
                                        AnimationTimeOfMouseIn),
                                    ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3",
                                        AnimationTimeOfMouseIn)
                                }, "MyIconTextButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty, "ColorBrushBg1", AnimationTimeOfMouseIn),
                                "MyIconTextButton Color " + Uuid);
                        }
                        else if (IsEnabled)
                        {
                            // 正常
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty, "ColorBrush3",
                                        AnimationTimeOfMouseOut),
                                    ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3",
                                        AnimationTimeOfMouseOut)
                                }, "MyIconTextButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty,
                                    ModSecret.ColorSemiTransparent - Background, AnimationTimeOfMouseOut),
                                "MyIconTextButton Color " + Uuid);
                        }
                        else
                        {
                            // 禁用
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty, "ColorBrushGray5", 100),
                                    ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrushGray5", 100)
                                }, "MyIconTextButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty,
                                    ModSecret.ColorSemiTransparent - Background, AnimationTimeOfMouseOut),
                                "MyIconTextButton Color " + Uuid);
                        }

                        break;
                    }
                }
            }

            else
            {
                // 不使用动画
                ModAnimation.AniStop("MyIconTextButton Checked " + Uuid);
                ModAnimation.AniStop("MyIconTextButton Color " + Uuid);
                switch (ColorType)
                {
                    case ColorState.Black:
                    {
                        Background = ModSecret.ColorSemiTransparent;
                        ShapeLogo.SetResourceReference(Shape.FillProperty,
                            IsEnabled ? "ColorBrush1" : "ColorBrushGray5");
                        LabText.SetResourceReference(TextBlock.ForegroundProperty,
                            IsEnabled ? "ColorBrush1" : "ColorBrushGray5");
                        break;
                    }
                    case ColorState.Highlight:
                    {
                        Background = ModSecret.ColorSemiTransparent;
                        ShapeLogo.SetResourceReference(Shape.FillProperty,
                            IsEnabled ? "ColorBrush3" : "ColorBrushGray5");
                        LabText.SetResourceReference(TextBlock.ForegroundProperty,
                            IsEnabled ? "ColorBrush3" : "ColorBrushGray5");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新带图标按钮颜色出错");
        }
    }
}