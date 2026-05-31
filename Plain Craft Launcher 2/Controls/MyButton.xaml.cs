using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace PCL;

[ContentProperty("Inlines")]
public partial class MyButton
{
    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e); // 自定义事件

    public enum ColorState
    {
        Normal = 0,
        Highlight = 1,
        Red = 2
    }

    // 自定义事件
    private const int animationColorIn = 100;
    private const int animationColorOut = 200;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyButton), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyButton)sender).LabText.Text = (string)e.NewValue;
        }));

    // 属性穿透
    public new static readonly DependencyProperty PaddingProperty = DependencyProperty.Register("Padding",
        typeof(Thickness), typeof(MyButton), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyButton)sender).PanFore.Padding = (Thickness)e.NewValue;
        }));
    
    private ColorState _ColorType = ColorState.Normal; // 配色方案

    // 鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    

    // 自定义属性
    public int uuid = ModBase.GetUuid();

    public MyButton()
    {
        InitializeComponent();

        MouseEnter += RefreshColor;
        MouseLeave += RefreshColor;
        Loaded += RefreshColor;
        IsEnabledChanged += (_, _) => RefreshColor();
        MouseLeftButtonUp += Button_MouseUp;
        MouseLeftButtonDown += Button_MouseDown;
        MouseEnter += (_, _) => Button_MouseEnter();
        MouseLeftButtonUp += (_, _) => Button_MouseUp();
        MouseLeave += (_, _) => Button_MouseLeave();
    }

    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    } // 显示文本

    public Thickness TextPadding
    {
        get => LabText.Padding;
        set => LabText.Padding = value;
    }

    public ColorState ColorType
    {
        get => _ColorType;
        set
        {
            _ColorType = value;
            RefreshColor();
        }
    }

    public new Thickness Padding
    {
        get => PanFore.Padding;
        set => PanFore.Padding = value;
    }

    public Transform RealRenderTransform
    {
        get => PanFore.RenderTransform;
        set => PanFore.RenderTransform = value;
    }

    // 声明
    public event ClickEventHandler? Click;

    private string GetBorderBrushResourceKey()
    {
        return ColorType switch
        {
            ColorState.Normal => IsMouseOver ? "ColorBrush3" : "ColorBrush1",
            ColorState.Highlight => IsMouseOver ? "ColorBrush3" : "ColorBrush2",
            ColorState.Red => IsMouseOver ? "ColorBrushRedLight" : "ColorBrushRedDark",
            _ => "ColorBrush1"
        };
    }

    private void StartBorderBrushAnimation(string resourceKey, int duration)
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaColor(PanFore, BorderBrushProperty, resourceKey, duration)
            }, "MyButton Color " + uuid);
    }

    private void RefreshColor(object obj = null, object e = null)
    {
        try
        {
            if (ControlVisualHelpers.ShouldAnimate(this)) // 防止默认属性变更触发动画
            {
                if (IsEnabled)
                    StartBorderBrushAnimation(GetBorderBrushResourceKey(), IsMouseOver ? animationColorIn : animationColorOut);
                else
                    // 不可用（Gray 4）
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaColor(PanFore, BorderBrushProperty,
                                ThemeManager.colorGray4 - PanFore.BorderBrush, animationColorOut)
                        }, "MyButton Color " + uuid);
            }
            else
            {
                ModAnimation.AniStop("MyButton Color " + uuid);
                if (IsEnabled)
                    PanFore.SetResourceReference(BorderBrushProperty, GetBorderBrushResourceKey());
                else
                    PanFore.BorderBrush = ThemeManager.colorGray4;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新按钮颜色出错");
        }
    }

    // 实现自定义事件
    private bool isMouseDown = false;
    private void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!isMouseDown)
            return;
        ModBase.Log("[Control] 按下按钮：" + Text);
        Click?.Invoke(sender, e);
        ModMain.RaiseCustomEvent(this);
    }

    private void Button_MouseDown(object sender, MouseButtonEventArgs e)
    {
        isMouseDown = true;
        Focus();
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanFore, 0.955d - ((ScaleTransform)PanFore.RenderTransform).ScaleX, 80,
                    ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                ModAnimation.AaScaleTransform(PanFore, -0.01d, 700, ease: new ModAnimation.AniEaseOutFluent())
            }, "MyButton Scale " + uuid);
    }

    private void Button_MouseEnter()
    {
        ModAnimation.AniStart(
            ModAnimation.AaColor(PanFore, BackgroundProperty,
                _ColorType == ColorState.Red ? "ColorBrushRedBack" : "ColorBrush7", animationColorIn),
            "MyButton Background " + uuid);
    }

    private void Button_MouseUp()
    {
        if (!isMouseDown)
            return;
        isMouseDown = false;
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanFore, 1d - ((ScaleTransform)PanFore.RenderTransform).ScaleX, 300, 10,
                    new ModAnimation.AniEaseOutFluent())
            }, "MyButton Scale " + uuid);
    }

    private void Button_MouseLeave()
    {
        ModAnimation.AniStart(
            ModAnimation.AaColor(PanFore, BackgroundProperty, "ColorBrushHalfWhite", animationColorOut),
            "MyButton Background " + uuid);
        if (!isMouseDown)
            return;
        isMouseDown = false;
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(PanFore, 1d - ((ScaleTransform)PanFore.RenderTransform).ScaleX, 800,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)), "MyButton Scale " + uuid);
    }
}
