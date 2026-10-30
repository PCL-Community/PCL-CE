using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

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

    // 鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    private bool isMouseDown;

    // 自定义属性

    public int Uuid = ModBase.GetUuid();

    protected override Size MeasureOverride(Size constraint)
    {
        var measured = base.MeasureOverride(constraint);
        if (double.IsNaN(Width) && !double.IsNaN(Height) && Height > 0D && !double.IsInfinity(Height))
            return new Size(Height, Height);
        return measured;
    }

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
        get => Path.Data?.ToString() ?? string.Empty;
        set
        {
            if (Path is null) return;
            Path.Data = (Geometry)new GeometryConverter().ConvertFromString(value);
            SvgIconControlHelper.ApplyVisibility(Path, ShapeSvgIcon, IsUsingSvgIcon);
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
            if (Path is null || ShapeSvgIcon is null)
                return;
            SvgIconControlHelper.ApplyIcon(Path, ShapeSvgIcon, field);
            ApplyLogoScale();
            RefreshAnim();
        }
    } = string.Empty;

    private bool IsUsingSvgIcon => SvgIconControlHelper.HasSvgIcon(SvgIcon);

    private double EffectiveLogoScale => IsUsingSvgIcon ? 1D : LogoScale;

    private void ApplyLogoScale()
    {
        IconHost?.RenderTransform = new ScaleTransform { ScaleX = EffectiveLogoScale, ScaleY = EffectiveLogoScale };
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

    public Themes Theme { get; set; } = Themes.Color;

    public SolidColorBrush Foreground
    {
        get;
        set
        {
            field = value;
            ModBase.BeginUiUpdate();
            RefreshAnim();
            ModBase.EndUiUpdate();
        }
    } = new(Color.FromRgb(128, 128, 128));

    // 自定义事件
    public event ClickEventHandler? Click;

    private static ModBase.MyColor GetTransparentBackground()
    {
        return new ModBase.MyColor(0d, 255d, 255d, 255d);
    }

    private ModBase.MyColor? GetBaseFillColor()
    {
        return Theme switch
        {
            Themes.Red => new ModBase.MyColor(160d, 255d, 76d, 76d),
            Themes.Black => ThemeManager.IsDarkMode
                ? new ModBase.MyColor(160d, 255d, 255d, 255d)
                : new ModBase.MyColor(160d, 0d, 0d, 0d),
            Themes.Custom => new ModBase.MyColor(160d, Foreground),
            _ => null
        };
    }

    private void EnsureBaseBrushes()
    {
        PanBack.Background ??= GetTransparentBackground();
        var baseFill = GetBaseFillColor();
        if (baseFill is not null && !IsUsingSvgIcon)
            Path.Fill ??= baseFill;
    }

    private void SetActiveIconResource(string resourceKey)
    {
        SvgIconControlHelper.SetIconResource(Path, ShapeSvgIcon, IsUsingSvgIcon, resourceKey);
    }

    private void SetActiveIconBrush(Brush brush)
    {
        SvgIconControlHelper.SetIconBrush(Path, ShapeSvgIcon, IsUsingSvgIcon, brush);
    }

    private void ApplyHoverTheme()
    {
        switch (Theme)
        {
            case Themes.Color:
                SetActiveIconResource("ColorBrush2");
                break;
            case Themes.White:
                PanBack.Background = new ModBase.MyColor(50d, 255d, 255d, 255d);
                break;
            case Themes.Red:
                SetActiveIconBrush(new ModBase.MyColor(255d, 76d, 76d));
                break;
            case Themes.Black:
                SetActiveIconBrush(ThemeManager.IsDarkMode
                    ? new ModBase.MyColor(230d, 255d, 255d, 255d)
                    : new ModBase.MyColor(230d, 0d, 0d, 0d));
                break;
            case Themes.Custom:
                SetActiveIconBrush(new ModBase.MyColor(255d, Foreground));
                break;
        }
    }

    private void ApplyNormalTheme()
    {
        switch (Theme)
        {
            case Themes.Color:
                SetActiveIconResource("ColorBrush4");
                break;
            case Themes.White:
                SetActiveIconBrush(new ModBase.MyColor(234d, 242d, 254d));
                break;
            case Themes.Red:
                SetActiveIconBrush(new ModBase.MyColor(160d, 255d, 76d, 76d));
                break;
            case Themes.Black:
                SetActiveIconBrush(ThemeManager.IsDarkMode
                    ? new ModBase.MyColor(160d, 255d, 255d, 255d)
                    : new ModBase.MyColor(160d, 0d, 0d, 0d));
                break;
            case Themes.Custom:
                SetActiveIconBrush(new ModBase.MyColor(160d, Foreground));
                break;
        }

        PanBack.Background = GetTransparentBackground();
    }

    private void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!isMouseDown)
            return;
        ModBase.Log("[Control] 按下图标按钮" + (string.IsNullOrEmpty(Name) ? "" : "：" + Name));
        Click?.Invoke(sender, e);
        e.Handled = true;
        Button_MouseUp();
        ModMain.RaiseCustomEvent(this);
    }

    private void Button_MouseDown(object sender, MouseButtonEventArgs e)
    {
        isMouseDown = true;
        Focus();
    }

    private void Button_MouseUp()
    {
        isMouseDown = false;
        RefreshAnim(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    private void Button_MouseLeave()
    {
        isMouseDown = false;
        RefreshAnim(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    public void RefreshAnim()
    {
        try
        {
            EnsureBaseBrushes();
            if (IsMouseOver)
                ApplyHoverTheme();
            else
                ApplyNormalTheme();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新图标按钮状态出错");
        }
    }

    /// <summary>
    ///     立即移除或隐藏图标按钮。
    /// </summary>
    public static void DisposeIconButton(MyIconButton control, bool removeFromChildren,
        Action<MyIconButton>? callBack = null)
    {
        if (!control.IsHitTestVisible)
            return;
        control.IsHitTestVisible = false;
        if (removeFromChildren)
            ((Panel)control.Parent).Children.Remove(control);
        else
            control.Visibility = Visibility.Collapsed;
        callBack?.Invoke(control);
    }
}