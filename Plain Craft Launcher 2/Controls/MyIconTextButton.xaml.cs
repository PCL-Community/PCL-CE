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

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyIconTextButton), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyIconTextButton)sender).LabText.Text = (string)e.NewValue;
        }));

    public static readonly DependencyProperty ColorTypeProperty = DependencyProperty.Register("ColorType",
        typeof(ColorState), typeof(MyIconTextButton), new PropertyMetadata(ColorState.Black));

    private bool _hasLegacyLogo;
    private bool isMouseDown;

    // 基础

    public int Uuid = ModBase.GetUuid();

    public MyIconTextButton()
    {
        InitializeComponent();
        RefreshLogoHostVisibility();

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
        get => ShapeLogo.Data?.ToString() ?? string.Empty;
        set
        {
            if (ShapeLogo is null) return;
            _hasLegacyLogo = !string.IsNullOrWhiteSpace(value);
            ShapeLogo.Data = _hasLegacyLogo
                ? (Geometry)new GeometryConverter().ConvertFromString(value)!
                : null;
            SvgIconControlHelper.ApplyVisibility(ShapeLogo, ShapeSvgIcon, IsUsingSvgIcon);
            RefreshLogoHostVisibility();
        }
    }

    public string SvgIcon
    {
        get;
        set
        {
            value ??= string.Empty;
            if (value == field)
                return;
            field = value;
            if (ShapeLogo is null || ShapeSvgIcon is null)
                return;
            SvgIconControlHelper.ApplyIcon(ShapeLogo, ShapeSvgIcon, field);
            ApplyLogoScale();
            RefreshLogoHostVisibility();
            RefreshColor();
        }
    } = string.Empty;

    private bool IsUsingSvgIcon => SvgIconControlHelper.HasSvgIcon(SvgIcon);

    private double EffectiveLogoScale => IsUsingSvgIcon ? 1D : LogoScale;

    private bool HasAnyIcon => IsUsingSvgIcon || _hasLegacyLogo;

    private void ApplyLogoScale()
    {
        LogoHost?.RenderTransform = new ScaleTransform
        {
            ScaleX = EffectiveLogoScale,
            ScaleY = EffectiveLogoScale
        };
    }

    private void RefreshLogoHostVisibility()
    {
        if (LogoHost is null || LabText is null)
            return;

        if (HasAnyIcon)
        {
            LogoHost.Visibility = Visibility.Visible;
            LogoHost.Width = 16;
            LogoHost.Height = 16;
            LogoHost.Margin = new Thickness(12, 0, 0, 0);
            LabText.Margin = new Thickness(7, 0, 12, 1);
        }
        else
        {
            LogoHost.Visibility = Visibility.Collapsed;
            LogoHost.Width = 0;
            LogoHost.Height = 16;
            LogoHost.Margin = new Thickness(0);
            LabText.Margin = new Thickness(12, 0, 12, 1);
        }
    }

    public double LogoScale
    {
        get;
        set
        {
            field = value;
            ApplyLogoScale();
        }
    } = 1d;

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

    private string GetDefaultForegroundResourceKey()
    {
        return ColorType == ColorState.Highlight ? "ColorBrush3" : "ColorBrush1";
    }

    private void SetForegroundResource(string resourceKey)
    {
        SvgIconControlHelper.SetIconResource(ShapeLogo, ShapeSvgIcon, IsUsingSvgIcon, resourceKey);
        LabText.SetResourceReference(TextBlock.ForegroundProperty, resourceKey);
    }

    private void SetBackgroundResource(string resourceKey)
    {
        SetResourceReference(BackgroundProperty, resourceKey);
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
            if (isMouseDown)
            {
                SetBackgroundResource("ColorBrush6");
            }
            else if (IsMouseOver)
            {
                SetForegroundResource("ColorBrush3");
                SetBackgroundResource("ColorBrushBg1");
            }
            else if (IsEnabled)
            {
                SetForegroundResource(GetDefaultForegroundResourceKey());
                Background = ThemeManager.colorSemiTransparent;
            }
            else
            {
                SetForegroundResource("ColorBrushGray5");
                Background = ThemeManager.colorSemiTransparent;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新带图标按钮颜色出错");
        }
    }
}
