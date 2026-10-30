using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.App.Localization;

namespace PCL;

public partial class MySlider
{
    public delegate void ChangeEventHandler(object sender, bool user);

    public delegate void PreviewChangeEventHandler(object sender, Common.RouteEventArgs e);

    // 自定义属性

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(int),
        typeof(MySlider), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) => ((MySlider)d).OnValueChanged((int)e.OldValue, (int)e.NewValue)));

    private bool _isUpdatingValue;
    private bool changeByKey;

    // 拖动

    public Delegate getHintText;

    // 基础

    public int Uuid = Common.GetUuid();

    public MySlider()
    {
        InitializeComponent();
        SizeChanged += RefreshWidth;
        MouseLeftButtonDown += DragStart;
        IsEnabledChanged += (_, _) => RefreshColor();
        MouseEnter += (_, _) => RefreshColor();
        MouseLeave += (_, _) => RefreshColor();
        MouseEnter += (_, _) => MySlider_MouseEnter();
        KeyDown += MySlider_KeyDown;
    }

    public int MaxValue
    {
        get => field;
        set
        {
            if (value == field)
                return;
            field = value;
            RefreshWidth(null, null);
        }
    } = 100;

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set
        {
            var clamped = (int)Math.Round(Common.MathClamp(value, 0d, MaxValue));
            if (clamped == Value)
                return;
            SetCurrentValue(ValueProperty, clamped);
        }
    }

    private void OnValueChanged(int oldValue, int newValue)
    {
        if (_isUpdatingValue)
            return;
        try
        {
            var value = (int)Math.Round(Common.MathClamp(newValue, 0d, MaxValue));
            if (value != newValue)
            {
                _isUpdatingValue = true;
                try
                {
                    SetCurrentValue(ValueProperty, value);
                }
                finally
                {
                    _isUpdatingValue = false;
                }
            }

            // 触发 Preview 事件，修改新值
            if (!Animations.AniExitsAnyControl())
            {
                var e = new Common.RouteEventArgs();
                PreviewChange?.Invoke(this, e);
                if (e.handled)
                {
                    _isUpdatingValue = true;
                    try
                    {
                        SetCurrentValue(ValueProperty, oldValue);
                    }
                    finally
                    {
                        _isUpdatingValue = false;
                    }

                    DragStop();
                    return;
                }
            }

            if (IsLoaded && !Animations.AniExitsAnyControl())
            {
                if (ActualWidth < ShapeDot.Width)
                    return;
                var newWidth = value / (double)MaxValue * (ActualWidth - ShapeDot.Width);
                var deltaProcess =
                    Math.Abs(LineFore.Width / (ActualWidth - ShapeDot.Width) - value / (double)MaxValue);
                var time = (1d - Math.Pow(1d - deltaProcess, 3d)) * 300d + (changeByKey ? 100 : 0);
                Animations.AniStart(
                    new[]
                    {
                        Animations.AaWidth(LineFore,
                            Math.Max(0d, newWidth + (newWidth < 0.5d ? 0d : 0.5d)) - LineFore.Width,
                            (int)Math.Round(time),
                            ease: time > 50d
                                ? new Animations.AniEaseOutFluent()
                                : new Animations.AniEaseLinear()),
                        Animations.AaWidth(LineBack,
                            Math.Max(0d,
                                ActualWidth - ShapeDot.Width - newWidth +
                                (ActualWidth - ShapeDot.Width - newWidth < 0.5d ? 0d : 0.5d)) - LineBack.Width,
                            (int)Math.Round(time),
                            ease: time > 50d
                                ? new Animations.AniEaseOutFluent()
                                : new Animations.AniEaseLinear()),
                        Animations.AaX(ShapeDot, newWidth - ShapeDot.Margin.Left,
                            (int)Math.Round(time),
                            ease: time > 50d
                                ? new Animations.AniEaseOutFluent()
                                : new Animations.AniEaseLinear())
                    }, "MySlider Progress " + Uuid);
            }
            else
            {
                RefreshWidth(null, null);
            }

            if (!Animations.AniExitsAnyControl())
                Change?.Invoke(this, false);
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                "滑动条进度改变出错",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.Error.OperationFailed"));
        }
    }

    // 按键改变

    public uint ValueByKey { get; set; } = 1U;
    public event ChangeEventHandler? Change;
    public event PreviewChangeEventHandler? PreviewChange;

    private void RefreshWidth(object sender, SizeChangedEventArgs? e)
    {
        if (e is not null)
            PanMain.Width = e.NewSize.Width;
        Animations.AniStop("MySlider Progress " + Uuid);
        var newWidth = Value / (double)MaxValue * (ActualWidth - ShapeDot.Width);
        LineFore.Width = Math.Max(0d, newWidth + (newWidth < 0.5d ? 0d : 0.5d));
        LineBack.Width = Math.Max(0d,
            ActualWidth - ShapeDot.Width - newWidth + (ActualWidth - ShapeDot.Width - newWidth < 0.5d ? 0d : 0.5d));
        Common.SetLeft(ShapeDot, newWidth);
    }

    private void DragStart(object sender, MouseButtonEventArgs e)
    {
        CaptureMouse();
        MouseMove += OnDragMouseMove;
        e.Handled = true; // 防止 ScrollViewer 失焦问题
        Launcher.dragControl = this;
        RefreshColor();
        Launcher.frmMain.DragDoing();
        Animations.AniStart(
            Animations.AaScaleTransform(ShapeDot,
                1.3d - ((ScaleTransform)ShapeDot.RenderTransform).ScaleX, 40,
                ease: new Animations.AniEaseOutFluent()), "MySlider Scale " + Uuid);
        RefreshPopup();
        Animations.AniStop("MySlider KeyPopup " + Uuid);
    }

    public void DragDoing()
    {
        var percent =
            Common.MathClamp(
                (Mouse.GetPosition(PanMain).X - ShapeDot.Width / 2d) /
                (ActualWidth - ShapeDot.Width), 0d,
                1d);
        var newValue = (int)Math.Round(percent * MaxValue);
        if (newValue != Value) Value = newValue;
        RefreshPopup();
    }
    
    private void OnDragMouseMove(object sender, MouseEventArgs e)
    {
        DragDoing();
    }
    
    public void DragStop()
    {
        MouseMove -= OnDragMouseMove;
        if (IsMouseCaptured) ReleaseMouseCapture();
        RefreshColor();
        Animations.AniStart(
            Animations.AaScaleTransform(ShapeDot,
                1d - ((ScaleTransform)ShapeDot.RenderTransform).ScaleX, 200,
                ease: new Animations.AniEaseOutFluent()), "MySlider Scale " + Uuid);
        Popup.IsOpen = false;
    }

    public void RefreshPopup()
    {
        if (getHintText is null)
            return;
        Popup.IsOpen = true;
        TextHint.Text = getHintText.DynamicInvoke(Value)?.ToString() ?? "";
        var typeface = new Typeface(TextHint.FontFamily, TextHint.FontStyle, TextHint.FontWeight, TextHint.FontStretch);
        var formattedText = new FormattedText(TextHint.Text, Thread.CurrentThread.CurrentCulture,
            TextHint.FlowDirection, typeface, TextHint.FontSize, TextHint.Foreground, Common.Dpi);
        TextHint.Width = formattedText.Width; // 使用手动测量的宽度修复 #1057
    }

    // 指向动画

    private void RefreshColor()
    {
        try
        {
            // 判断当前颜色
            string foregroundName;
            string dotFillName;
            int animationTime;
            if (IsEnabled)
            {
                if ((Launcher.dragControl is not null && Launcher.dragControl.Equals(this)) ||
                    IsMouseOver)
                {
                    foregroundName = "ColorBrush3";
                    dotFillName = "ColorBrush3";
                    animationTime = 40;
                }
                else
                {
                    foregroundName = "ColorBrushBg0";
                    dotFillName = "ColorBrushBg0";
                    animationTime = 100;
                }
            }
            else
            {
                foregroundName = "ColorBrushGray5";
                dotFillName = "ColorBrushGray5";
                animationTime = 200;
            }

            // 触发颜色动画
            if (IsLoaded && !Animations.AniExitsAnyControl()) // 防止默认属性变更触发动画
            {
                // 有动画
                Animations.AniStart(
                    new[]
                    {
                        Animations.AaColor(this, BorderBrushProperty, foregroundName,
                            animationTime),
                        Animations.AaColor(ShapeDot, Shape.FillProperty, dotFillName, animationTime)
                    }, "MySlider Color " + Uuid);
            }
            else
            {
                // 无动画
                Animations.AniStop("MySlider Color " + Uuid);
                SetResourceReference(BorderBrushProperty, foregroundName);
                ShapeDot.SetResourceReference(Shape.FillProperty, dotFillName);
            }
        }

        catch (Exception ex)
        {
            Common.Log(ex, "滑动条颜色改变出错");
        }
    }

    private void MySlider_MouseEnter()
    {
        Focus(); // 确保按键能改变值
    }

    private void MySlider_KeyDown(object sender, KeyEventArgs e)
    {
        // 拒绝一边拖动一边用按键改变
        if (ReferenceEquals(this, Launcher.dragControl))
            return;
        // 改变值
        if (e.Key == Key.Left)
        {
            changeByKey = true;
            Value = (int)(Value - ValueByKey);
            changeByKey = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            changeByKey = true;
            Value = (int)(Value + ValueByKey);
            changeByKey = false;
            e.Handled = true;
        }
        else
        {
            return;
        }

        // 更新 Popup
        if (getHintText is not null)
        {
            RefreshPopup();
            Animations.AniStop("MySlider KeyPopup " + Uuid);
            Animations.AniStart(
                Animations.AaCode(() => Popup.IsOpen = false,
                    (int)Math.Round(700d * Animations.aniSpeed)),
                "MySlider KeyPopup " + Uuid);
        }
    }
}
