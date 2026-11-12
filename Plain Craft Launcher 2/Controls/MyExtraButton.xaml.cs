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
    // 务必放在 IsMouseDown 更新之后
    private const int animationColorIn = 120;
    private const int animationColorOut = 150;

    // 鼠标点击判定（务必放在点击事件之后，以使得 Button_MouseUp 先于 Button_MouseLeave 执行）
    private bool isLeftMouseHeld;
    private bool isRightMouseHeld;
    public ShowCheckDelegate showCheck = null;

    // 自定义属性
    public int Uuid = Common.GetUuid();

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command),
        typeof(ICommand), typeof(MyExtraButton), new PropertyMetadata(null));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter), typeof(object), typeof(MyExtraButton), new PropertyMetadata(null));

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public static readonly DependencyProperty RightClickCommandProperty = DependencyProperty.Register(
        nameof(RightClickCommand), typeof(ICommand), typeof(MyExtraButton), new PropertyMetadata(null));

    public ICommand? RightClickCommand
    {
        get => (ICommand?)GetValue(RightClickCommandProperty);
        set => SetValue(RightClickCommandProperty, value);
    }

    public MyExtraButton()
    {
        Loaded += (_, _) => RefreshColor();
        IsEnabledChanged += (_, _) => RefreshColor();
        InitializeComponent();
        PanClick.MouseLeave += (_, _) => Button_MouseLeave();
    }

    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress),
        typeof(double), typeof(MyExtraButton), new PropertyMetadata(0d, OnProgressChanged));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set
        {
            if (Progress == value)
                return;
            SetValue(ProgressProperty, value);
        }
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MyExtraButton)d;
        var value = (double)e.NewValue;
        if (value < 0.0001d)
        {
            control.PanProgress.Visibility = Visibility.Collapsed;
        }
        else
        {
            control.PanProgress.Visibility = Visibility.Visible;
            control.RectProgress.Rect = new Rect(0d, 40d * (1d - value), 40d, 40d * value);
        }
    }

    public static readonly DependencyProperty LogoProperty = DependencyProperty.Register(nameof(Logo),
        typeof(string), typeof(MyExtraButton), new PropertyMetadata("", OnLogoChanged));

    public string Logo
    {
        get => (string)GetValue(LogoProperty);
        set
        {
            if ((value ?? "") == (Logo ?? ""))
                return;
            SetValue(LogoProperty, value);
        }
    }

    private static void OnLogoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MyExtraButton)d;
        var value = e.NewValue as string;
        if ((value ?? "") == ((string)e.OldValue ?? ""))
            return;
        control.Path.Data = (Geometry)new GeometryConverter().ConvertFromString(value);
        SvgIconControlHelper.ApplyVisibility(control.Path, control.ShapeSvgIcon, control.IsUsingSvgIcon);
    }

    public static readonly DependencyProperty SvgIconProperty = DependencyProperty.Register(nameof(SvgIcon),
        typeof(string), typeof(MyExtraButton), new PropertyMetadata(string.Empty, OnSvgIconChanged));

    public string SvgIcon
    {
        get => (string)GetValue(SvgIconProperty);
        set
        {
            value ??= string.Empty;
            if (value == SvgIcon)
                return;
            SetValue(SvgIconProperty, value);
        }
    }

    private static void OnSvgIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MyExtraButton)d;
        var value = (string)e.NewValue ?? string.Empty;
        if (value == ((string)e.OldValue ?? string.Empty))
            return;
        if (control.Path is null || control.ShapeSvgIcon is null)
            return;
        SvgIconControlHelper.ApplyIcon(control.Path, control.ShapeSvgIcon, value);
        control.ApplyLogoScale();
    }

    private bool IsUsingSvgIcon => SvgIconControlHelper.HasSvgIcon(SvgIcon);

    private double EffectiveLogoScale => IsUsingSvgIcon ? 1D : LogoScale;

    public static readonly DependencyProperty LogoScaleProperty = DependencyProperty.Register(nameof(LogoScale),
        typeof(double), typeof(MyExtraButton), new PropertyMetadata(1d, OnLogoScaleChanged));

    public double LogoScale
    {
        get => (double)GetValue(LogoScaleProperty);
        set
        {
            var oldValue = LogoScale;
            SetValue(LogoScaleProperty, value);
            if (Equals(oldValue, value))
                ApplyLogoScale();
        }
    }

    private static void OnLogoScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MyExtraButton)d).ApplyLogoScale();

    public static readonly DependencyProperty ShowProperty = DependencyProperty.Register(nameof(Show),
        typeof(bool), typeof(MyExtraButton), new PropertyMetadata(false, OnShowChanged));

    public bool Show
    {
        get => (bool)GetValue(ShowProperty);
        set => Common.RunInUi(() => SetValue(ShowProperty, value));
    }

    private static void OnShowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MyExtraButton)d;
        var value = (bool)e.NewValue;
        Common.RunInUi(() =>
        {
            if (value)
            {
                // 有了
                control.Visibility = Visibility.Visible;
                Animations.AniStart(
                    new[]
                    {
                        Animations.AaScaleTransform(control,
                            0.3d - ((ScaleTransform)control.RenderTransform).ScaleX, 500,
                            60, new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak)),
                        Animations.AaScaleTransform(control, 0.7d, 500, 60,
                            new Animations.AniEaseOutBack(Animations.AniEasePower.Weak)),
                        Animations.AaHeight(control, 50d - control.Height, 200,
                            ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak))
                    }, "MyExtraButton MainScale " + control.Uuid);
            }
            else
            {
                // 没了
                Animations.AniStart(
                    new[]
                    {
                        Animations.AaScaleTransform(control,
                            -((ScaleTransform)control.RenderTransform).ScaleX, 100,
                            ease: new Animations.AniEaseInFluent(Animations.AniEasePower.Weak)),
                        Animations.AaHeight(control, -control.Height, 400, 100,
                            new Animations.AniEaseOutFluent()),
                        Animations.AaCode(() => control.Visibility = Visibility.Collapsed, after: true)
                    }, "MyExtraButton MainScale " + control.Uuid);
            }

            control.IsHitTestVisible = value; // 防止缩放动画中依然可以点进去
        });
    }

    public bool CanRightClick { get; set; }

    private void ApplyLogoScale()
    {
        IconHost?.RenderTransform = new ScaleTransform { ScaleX = EffectiveLogoScale, ScaleY = EffectiveLogoScale };
    }

    // 声明
    public event ClickEventHandler? Click;
    public event RightClickEventHandler? RightClick;

    private void StartScaleAnimation(double targetScale, double reboundScale, int reboundDuration = 60)
    {
        Animations.AniStart(
            new[]
            {
                Animations.AaScaleTransform(PanScale,
                    targetScale - ((ScaleTransform)PanScale.RenderTransform).ScaleX,
                    800, ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Strong)),
                Animations.AaScaleTransform(PanScale, reboundScale, reboundDuration,
                    ease: new Animations.AniEaseOutFluent())
            }, "MyExtraButton Scale " + Uuid);
    }

    private void RefreshScaleAfterRelease()
    {
        Animations.AniStart(
            new[]
            {
                Animations.AaScaleTransform(PanScale,
                    1d - ((ScaleTransform)PanScale.RenderTransform).ScaleX, 300,
                    ease: new Animations.AniEaseOutBack())
            }, "MyExtraButton Scale " + Uuid);
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
            Common.Log("[Control] 按下附加按钮" +
                        (ToolTip is null or "" ? "" : "：" + ToolTip));
            Click?.Invoke(sender, e);
            e.Handled = true;
            Button_LeftMouseUp();
            ExecuteCommand();
        }
    }

    private void Button_RightMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (isRightMouseHeld)
        {
            Common.Log("[Control] 右键按下附加按钮" +
                        (ToolTip is null or "" ? "" : "：" + ToolTip));
            RightClick?.Invoke(sender, e);
            e.Handled = true;
            Button_RightMouseUp();
            ExecuteRightClickCommand();
        }
    }

    private void Button_LeftMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!isLeftMouseHeld && !isRightMouseHeld)
            StartScaleAnimation(0.85d, -0.05d);
        isLeftMouseHeld = true;
        Focus();
    }

    private void Button_RightMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!CanRightClick)
            return;
        if (!isLeftMouseHeld && !isRightMouseHeld)
            StartScaleAnimation(0.85d, -0.05d);
        isRightMouseHeld = true;
        Focus();
    }

    private void Button_LeftMouseUp()
    {
        if (!isRightMouseHeld)
            RefreshScaleAfterRelease();
        if (isLeftMouseHeld) Launcher.RaiseCustomEvent(this);
        isLeftMouseHeld = false;
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    private void Button_RightMouseUp()
    {
        if (!CanRightClick)
            return;
        if (!isLeftMouseHeld)
            RefreshScaleAfterRelease();
        isRightMouseHeld = false;
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    private void Button_MouseLeave()
    {
        isLeftMouseHeld = false;
        isRightMouseHeld = false;
        Animations.AniStart(
            new[]
            {
                Animations.AaScaleTransform(PanScale,
                    1d - ((ScaleTransform)PanScale.RenderTransform).ScaleX, 500,
                    ease: new Animations.AniEaseOutFluent())
            }, "MyExtraButton Scale " + Uuid);
        RefreshColor(); // 直接刷新颜色以判断是否已触发 MouseLeave
    }

    public void RefreshColor()
    {
        try
        {
            if (ControlVisualHelpers.ShouldAnimate(this)) // 防止默认属性变更触发动画
            {
                if (!IsEnabled)
                    // 禁用
                    Animations.AniStart(
                        Animations.AaColor(PanColor, BackgroundProperty, "ColorBrushGray4",
                            animationColorIn),
                        "MyExtraButton Color " + Uuid);
                else if (IsMouseOver)
                    // 指向
                    Animations.AniStart(
                        Animations.AaColor(PanColor, BackgroundProperty, "ColorBrush4",
                            animationColorIn),
                        "MyExtraButton Color " + Uuid);
                else
                    // 普通
                    Animations.AniStart(
                        Animations.AaColor(PanColor, BackgroundProperty, "ColorBrush3",
                            animationColorOut),
                        "MyExtraButton Color " + Uuid);
            }

            else
            {
                ControlVisualHelpers.AnimateColorOrSetResource(PanColor, BackgroundProperty,
                    !IsEnabled ? "ColorBrushGray4" : IsMouseOver ? "ColorBrush4" : "ColorBrush3",
                    !IsEnabled || IsMouseOver ? animationColorIn : animationColorOut,
                    "MyExtraButton Color " + Uuid, false);
            }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "刷新图标按钮颜色出错");
        }
    }

    /// <summary>
    ///     发出一圈波浪效果提示。
    /// </summary>
    public void Ribble()
    {
        Common.RunInUi(() =>
        {
            var shape = new Border
            {
                CornerRadius = new CornerRadius(1000d), BorderThickness = new Thickness(0.001d), Opacity = 0.5d,
                RenderTransformOrigin = new Point(0.5d, 0.5d), RenderTransform = new ScaleTransform()
            };
            shape.SetResourceReference(Border.BackgroundProperty, "ColorBrush5");
            PanScale.Children.Insert(0, shape);
            Animations.AniStart(
                new[]
                {
                    Animations.AaScaleTransform(shape, 13d, 1000,
                        ease: new Animations.AniEaseInoutFluent(Animations.AniEasePower.Strong,
                            0.3d)),
                    Animations.AaOpacity(shape, -shape.Opacity, 1000),
                    Animations.AaCode(() => PanScale.Children.Remove(shape), after: true)
                }, "ExtraButton Ribble " + Common.GetUuid());
        });
    }

    private void PanClick_MouseEvent(object sender, MouseEventArgs e)
    {
        RefreshColor();
    }

    private bool _isCommandRunning;

    private void ExecuteCommand()
    {
        var command = Command;
        if (_isCommandRunning || command is null || !command.CanExecute(CommandParameter))
            return;
        _isCommandRunning = true;
        try
        {
            command.Execute(CommandParameter);
        }
        finally
        {
            _isCommandRunning = false;
        }
    }

    private bool _isRightClickCommandRunning;

    private void ExecuteRightClickCommand()
    {
        var command = RightClickCommand;
        if (_isRightClickCommandRunning || command is null || !command.CanExecute(CommandParameter))
            return;
        _isRightClickCommandRunning = true;
        try
        {
            command.Execute(CommandParameter);
        }
        finally
        {
            _isRightClickCommandRunning = false;
        }
    }
}