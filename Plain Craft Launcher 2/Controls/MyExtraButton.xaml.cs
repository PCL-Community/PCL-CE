using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCL;

public partial class MyExtraButton
{
    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e); // 自定义事件

    public delegate void RightClickEventHandler(object sender, MouseButtonEventArgs e);

    public delegate bool ShowCheckDelegate();

    // 自定义事件

    // 鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    private bool isLeftMouseHeld;
    private bool isRightMouseHeld;
    public ShowCheckDelegate showCheck = null;

    // 自定义属性
    public int Uuid = ModBase.GetUuid();

    public MyExtraButton()
    {
        Loaded += (_, _) => RefreshColor();
        IsEnabledChanged += (_, _) => RefreshColor();
        InitializeComponent();
        PanClick.MouseLeave += (_, _) => Button_MouseLeave();
    }

    public double Progress
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            if (value < 0.0001d)
            {
                PanProgress.Visibility = Visibility.Collapsed;
            }
            else
            {
                PanProgress.Visibility = Visibility.Visible;
                RectProgress.Rect = new Rect(0d, 40d * (1d - value), 40d, 40d * value);
            }
        }
    }

    public string Logo
    {
        get;
        set
        {
            if ((value ?? "") == (field ?? ""))
                return;
            field = value;
            Path.Data = (Geometry)new GeometryConverter().ConvertFromString(value);
            SvgIconControlHelper.ApplyVisibility(Path, ShapeSvgIcon, IsUsingSvgIcon);
        }
    } = "";

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
        }
    } = string.Empty;

    private bool IsUsingSvgIcon => SvgIconControlHelper.HasSvgIcon(SvgIcon);

    private double EffectiveLogoScale => IsUsingSvgIcon ? 1D : LogoScale;

    public double LogoScale
    {
        get;
        set
        {
            field = value;
            ApplyLogoScale();
        }
    } = 1d;

    public bool Show
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            ModBase.RunInUi(() =>
            {
                if (value)
                {
                    // 有了
                    Visibility = Visibility.Visible;
                    Height = 50d;
                    ApplyMainScale(1d);
                }
                else
                {
                    // 没了
                    Height = 0d;
                    ApplyMainScale(0d);
                    Visibility = Visibility.Collapsed;
                }

                IsHitTestVisible = value; // 同步可否点击状态
            });
        }
    }

    public bool CanRightClick { get; set; }

    private void ApplyLogoScale()
    {
        IconHost?.RenderTransform = new ScaleTransform { ScaleX = EffectiveLogoScale, ScaleY = EffectiveLogoScale };
    }

    // 声明
    public event ClickEventHandler? Click;
    public event RightClickEventHandler? RightClick;

    private void ApplyMainScale(double scale)
    {
        if (RenderTransform is ScaleTransform transform)
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
        }
    }

    private void ApplyPanScale(double scale)
    {
        if (PanScale.RenderTransform is ScaleTransform transform)
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
        }
    }

    public void ShowRefresh()
    {
        if (showCheck is not null)
            Show = showCheck();
    }

    // 触发点击事件
    private void Button_LeftMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (isLeftMouseHeld)
        {
            ModBase.Log("[Control] 按下附加按钮" +
                        (ToolTip is null or "" ? "" : "：" + ToolTip));
            Click?.Invoke(sender, e);
            e.Handled = true;
            Button_LeftMouseUp();
        }
    }

    private void Button_RightMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (isRightMouseHeld)
        {
            ModBase.Log("[Control] 右键按下附加按钮" +
                        (ToolTip is null or "" ? "" : "：" + ToolTip));
            RightClick?.Invoke(sender, e);
            e.Handled = true;
            Button_RightMouseUp();
        }
    }

    private void Button_LeftMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!isLeftMouseHeld && !isRightMouseHeld)
            ApplyPanScale(0.8d);
        isLeftMouseHeld = true;
        Focus();
    }

    private void Button_RightMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!CanRightClick)
            return;
        if (!isLeftMouseHeld && !isRightMouseHeld)
            ApplyPanScale(0.8d);
        isRightMouseHeld = true;
        Focus();
    }

    private void Button_LeftMouseUp()
    {
        if (!isRightMouseHeld)
            ApplyPanScale(1d);
        if (isLeftMouseHeld) ModMain.RaiseCustomEvent(this);
        isLeftMouseHeld = false;
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    private void Button_RightMouseUp()
    {
        if (!CanRightClick)
            return;
        if (!isLeftMouseHeld)
            ApplyPanScale(1d);
        isRightMouseHeld = false;
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    private void Button_MouseLeave()
    {
        isLeftMouseHeld = false;
        isRightMouseHeld = false;
        ApplyPanScale(1d);
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    public void RefreshColor()
    {
        try
        {
            PanColor.SetResourceReference(BackgroundProperty,
                !IsEnabled ? "ColorBrushGray4" : IsMouseOver ? "ColorBrush4" : "ColorBrush3");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新图标按钮颜色出错");
        }
    }

    private void PanClick_MouseEvent(object sender, MouseEventArgs e)
    {
        RefreshColor();
    }
}