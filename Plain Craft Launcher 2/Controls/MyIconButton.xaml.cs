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

    // 务必放在 IsMouseDown 更新之后
    private const int animationColorIn = 120;
    private const int animationColorOut = 150;

    //鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
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
            ModAnimation.AniControlEnabled += 1;
            RefreshAnim();
            ModAnimation.AniControlEnabled -= 1;
        }
    } = new(Color.FromRgb(128, 128, 128));

    private string ColorAnimationKey => "MyIconButton Color " + Uuid;

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

    private void AnimateActiveSvgIconBrush(string resourceKey, int duration)
    {
        if (IsUsingSvgIcon)
            SvgIconControlHelper.AnimateSvgIconBrushTo(ShapeSvgIcon, resourceKey, duration, ColorAnimationKey);
    }

    private void AnimateActiveSvgIconBrush(ModBase.MyColor color, int duration)
    {
        if (IsUsingSvgIcon)
            SvgIconControlHelper.AnimateSvgIconBrushTo(ShapeSvgIcon, color, duration, ColorAnimationKey);
    }

    private void SetActiveIconResource(string resourceKey)
    {
        SvgIconControlHelper.SetIconResource(Path, ShapeSvgIcon, IsUsingSvgIcon, resourceKey);
    }

    private void SetActiveIconBrush(Brush brush)
    {
        SvgIconControlHelper.SetIconBrush(Path, ShapeSvgIcon, IsUsingSvgIcon, brush);
    }

    private List<ModAnimation.AniData> GetHoverAnimations()
    {
        var animations = new List<ModAnimation.AniData>();
        switch (Theme)
        {
            case Themes.Color:
            {
                if (IsUsingSvgIcon)
                    AnimateActiveSvgIconBrush("ColorBrush2", animationColorIn);
                else
                    animations.Add(ModAnimation.AaColor(
                        Path,
                        Shape.FillProperty,
                        "ColorBrush2",
                        animationColorIn));

                break;
            }
            case Themes.White:
            {
                animations.Add(ModAnimation.AaColor(
                    PanBack,
                    BackgroundProperty,
                    new ModBase.MyColor(50d, 255d, 255d, 255d) - PanBack.Background,
                    animationColorIn));
                break;
            }
            case Themes.Red:
            {
                if (IsUsingSvgIcon)
                    AnimateActiveSvgIconBrush(
                        new ModBase.MyColor(255d, 76d, 76d),
                        animationColorIn);
                else
                    animations.Add(ModAnimation.AaColor(
                        Path,
                        Shape.FillProperty,
                        new ModBase.MyColor(255d, 76d, 76d) - Path.Fill,
                        animationColorIn));
                break;
            }
            case Themes.Black:
            {
                var blackHoverColor = ThemeManager.IsDarkMode
                    ? new ModBase.MyColor(230d, 255d, 255d, 255d)
                    : new ModBase.MyColor(230d, 0d, 0d, 0d);
                if (IsUsingSvgIcon)
                    AnimateActiveSvgIconBrush(blackHoverColor, animationColorIn);
                else
                    animations.Add(ModAnimation.AaColor(
                        Path,
                        Shape.FillProperty,
                        blackHoverColor - Path.Fill,
                        animationColorIn));
                break;
            }
            case Themes.Custom:
            {
                var customHoverColor = new ModBase.MyColor(255d, Foreground);
                if (IsUsingSvgIcon)
                    AnimateActiveSvgIconBrush(customHoverColor, animationColorIn);
                else
                    animations.Add(ModAnimation.AaColor(
                        Path,
                        Shape.FillProperty,
                        customHoverColor - Path.Fill,
                        animationColorIn));
                break;
            }
        }

        return animations;
    }

    private List<ModAnimation.AniData> GetNormalAnimations()
    {
        var animations = new List<ModAnimation.AniData>();
        switch (Theme)
        {
            case Themes.Color:
            {
                if (IsUsingSvgIcon)
                    AnimateActiveSvgIconBrush("ColorBrush4", animationColorOut);
                else
                    animations.Add(ModAnimation.AaColor(
                        Path,
                        Shape.FillProperty,
                        "ColorBrush4",
                        animationColorOut));

                PanBack.Background = GetTransparentBackground();
                break;
            }
            case Themes.White:
            {
                var whiteNormalColor = new ModBase.MyColor(234d, 242d, 254d);
                if (IsUsingSvgIcon)
                    AnimateActiveSvgIconBrush(whiteNormalColor, animationColorOut);
                else
                    animations.Add(ModAnimation.AaColor(
                        Path,
                        Shape.FillProperty,
                        whiteNormalColor,
                        animationColorOut));

                animations.Add(ModAnimation.AaColor(
                    PanBack,
                    BackgroundProperty,
                    GetTransparentBackground() - PanBack.Background,
                    animationColorOut));
                break;
            }
            case Themes.Red:
            {
                var redNormalColor = new ModBase.MyColor(160d, 255d, 76d, 76d);
                if (IsUsingSvgIcon)
                    AnimateActiveSvgIconBrush(redNormalColor, animationColorOut);
                else
                    animations.Add(ModAnimation.AaColor(
                        Path,
                        Shape.FillProperty,
                        redNormalColor - Path.Fill,
                        animationColorOut));

                PanBack.Background = GetTransparentBackground();
                break;
            }
            case Themes.Black:
            {
                var blackNormalColor = ThemeManager.IsDarkMode
                    ? new ModBase.MyColor(160d, 255d, 255d, 255d)
                    : new ModBase.MyColor(160d, 0d, 0d, 0d);
                if (IsUsingSvgIcon)
                    AnimateActiveSvgIconBrush(blackNormalColor, animationColorOut);
                else
                    animations.Add(ModAnimation.AaColor(
                        Path, 
                        Shape.FillProperty,
                        blackNormalColor - Path.Fill,
                        animationColorOut));

                PanBack.Background = GetTransparentBackground();
                break;
            }
            case Themes.Custom:
            {
                var customNormalColor = new ModBase.MyColor(160d, Foreground);
                if (IsUsingSvgIcon)
                    AnimateActiveSvgIconBrush(customNormalColor, animationColorOut);
                else
                    animations.Add(ModAnimation.AaColor(
                        Path,
                        Shape.FillProperty,
                        customNormalColor - Path.Fill,
                        animationColorOut));

                PanBack.Background = GetTransparentBackground();
                break;
            }
        }

        return animations;
    }

    private void ApplyNonAnimatedTheme()
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
        // 指向
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(PanBack, 0.8d - ((ScaleTransform)PanBack.RenderTransform).ScaleX,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
            "MyIconButton Scale " + Uuid);
    }

    private void Button_MouseUp()
    {
        if (isMouseDown)
        {
            isMouseDown = false;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaScaleTransform(PanBack, 1.05d - ((ScaleTransform)PanBack.RenderTransform).ScaleX,
                        250, ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaScaleTransform(PanBack, -0.05d, 250,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong))
                }, "MyIconButton Scale " + Uuid);
        }

        RefreshAnim(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    private void Button_MouseLeave()
    {
        isMouseDown = false;
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(PanBack, 1d - ((ScaleTransform)PanBack.RenderTransform).ScaleX, 250,
                    ease: new ModAnimation.AniEaseOutFluent())
            }, "MyIconButton Scale " + Uuid);
        RefreshAnim(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    public void RefreshAnim()
    {
        try
        {
            if (ControlVisualHelpers.ShouldAnimate(this)) // 防止默认属性变更触发动画
            {
                EnsureBaseBrushes();
                ModAnimation.AniStart(IsMouseOver ? GetHoverAnimations() : GetNormalAnimations(), ColorAnimationKey);
            }

            else
            {
                ModAnimation.AniStop(ColorAnimationKey);
                ApplyNonAnimatedTheme();
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
    public static void AniDispose(MyIconButton control, bool removeFromChildren,
        ParameterizedThreadStart callBack = null)
    {
        if (!control.IsHitTestVisible)
            return;
        control.IsHitTestVisible = false;
        AniStart(new[]
        {
            AaScaleTransform(control, -1.5d, 200, ease: new AniEaseInFluent()),
            AaCode(() =>
            {
                if (removeFromChildren)
                    ((Panel)control.Parent).Children.Remove(control);
                else
                    control.Visibility = Visibility.Collapsed;
                if (callBack is not null)
                    callBack(control);
            }, after: true)
        }, "MyIconButton Dispose " + control.Uuid);
    }
}