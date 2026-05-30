using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCL;

public partial class MySlider
{
    public delegate void ChangeEventHandler(object sender, bool user);

    public delegate void PreviewChangeEventHandler(object sender, ModBase.RouteEventArgs e);

    // 自定义属性

    private int _MaxValue = 100;
    private int _Value;
    private bool ChangeByKey;

    // 拖动

    public Delegate GetHintText;

    // 基础

    public int Uuid = ModBase.GetUuid();

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
        get => _MaxValue;
        set
        {
            if (value == _MaxValue)
                return;
            _MaxValue = value;
            RefreshWidth(null, null);
        }
    }

    public int Value
    {
        get => _Value;
        set
        {
            try
            {
                value = (int)Math.Round(ModBase.MathClamp(value, 0d, MaxValue));
                if (_Value == value)
                    return;

                // 触发 Preview 事件，修改新值
                var OldValue = _Value;
                _Value = value;
                if (ModAnimation.AniControlEnabled == 0)
                {
                    var e = new ModBase.RouteEventArgs();
                    PreviewChange?.Invoke(this, e);
                    if (e.Handled)
                    {
                        _Value = OldValue;
                        DragStop();
                        return;
                    }
                }

                if (IsLoaded && ModAnimation.AniControlEnabled == 0)
                {
                    if (ActualWidth < ShapeDot.Width)
                        return;
                    var NewWidth = _Value / (double)MaxValue * (ActualWidth - ShapeDot.Width);
                    var DeltaProcess =
                        Math.Abs(LineFore.Width / (ActualWidth - ShapeDot.Width) - _Value / (double)MaxValue);
                    var Time = (1d - Math.Pow(1d - DeltaProcess, 3d)) * 300d + (ChangeByKey ? 100 : 0);
                    ModAnimation.AniStart(
                        new[]
                        {
                            ModAnimation.AaWidth(LineFore,
                                Math.Max(0d, NewWidth + (NewWidth < 0.5d ? 0d : 0.5d)) - LineFore.Width,
                                (int)Math.Round(Time),
                                Ease: Time > 50d
                                    ? new ModAnimation.AniEaseOutFluent()
                                    : new ModAnimation.AniEaseLinear()),
                            ModAnimation.AaWidth(LineBack,
                                Math.Max(0d,
                                    ActualWidth - ShapeDot.Width - NewWidth +
                                    (ActualWidth - ShapeDot.Width - NewWidth < 0.5d ? 0d : 0.5d)) - LineBack.Width,
                                (int)Math.Round(Time),
                                Ease: Time > 50d
                                    ? new ModAnimation.AniEaseOutFluent()
                                    : new ModAnimation.AniEaseLinear()),
                            ModAnimation.AaX(ShapeDot, NewWidth - ShapeDot.Margin.Left, (int)Math.Round(Time),
                                Ease: Time > 50d
                                    ? new ModAnimation.AniEaseOutFluent()
                                    : new ModAnimation.AniEaseLinear())
                        }, "MySlider Progress " + Uuid);
                }
                else
                {
                    RefreshWidth(null, null);
                }

                if (ModAnimation.AniControlEnabled == 0)
                    Change?.Invoke(this, false);
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "滑动条进度改变出错", ModBase.LogLevel.Hint);
            }
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
        ModAnimation.AniStop("MySlider Progress " + Uuid);
        var NewWidth = _Value / (double)MaxValue * (ActualWidth - ShapeDot.Width);
        LineFore.Width = Math.Max(0d, NewWidth + (NewWidth < 0.5d ? 0d : 0.5d));
        LineBack.Width = Math.Max(0d,
            ActualWidth - ShapeDot.Width - NewWidth + (ActualWidth - ShapeDot.Width - NewWidth < 0.5d ? 0d : 0.5d));
        ModBase.SetLeft(ShapeDot, NewWidth);
    }

    private void DragStart(object sender, MouseButtonEventArgs e)
    {
        CaptureMouse();
        MouseMove += OnDragMouseMove;
        e.Handled = true; // 防止 ScrollViewer 失焦问题
        ModMain.DragControl = this;
        RefreshColor();
        ModMain.FrmMain.DragDoing();
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(ShapeDot, 1.3d - ((ScaleTransform)ShapeDot.RenderTransform).ScaleX, 40,
                Ease: new ModAnimation.AniEaseOutFluent()), "MySlider Scale " + Uuid);
        RefreshPopup();
        ModAnimation.AniStop("MySlider KeyPopup " + Uuid);
    }

    public void DragDoing()
    {
        var Percent =
            ModBase.MathClamp((Mouse.GetPosition(PanMain).X - ShapeDot.Width / 2d) / (ActualWidth - ShapeDot.Width), 0d,
                1d);
        var NewValue = (int)Math.Round(Percent * MaxValue);
        if (NewValue != Value) Value = NewValue;
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
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(ShapeDot, 1d - ((ScaleTransform)ShapeDot.RenderTransform).ScaleX, 200,
                Ease: new ModAnimation.AniEaseOutFluent()), "MySlider Scale " + Uuid);
        Popup.IsOpen = false;
    }

    public void RefreshPopup()
    {
        if (GetHintText is null)
            return;
        Popup.IsOpen = true;
        TextHint.Text = GetHintText.DynamicInvoke(Value)?.ToString() ?? "";
        var typeface = new Typeface(TextHint.FontFamily, TextHint.FontStyle, TextHint.FontWeight, TextHint.FontStretch);
        var formattedText = new FormattedText(TextHint.Text, Thread.CurrentThread.CurrentCulture,
            TextHint.FlowDirection, typeface, TextHint.FontSize, TextHint.Foreground, ModBase.DPI);
        TextHint.Width = formattedText.Width; // 使用手动测量的宽度修复 #1057
    }

    // 指向动画

    private void RefreshColor()
    {
        try
        {
            // 判断当前颜色
            string ForegroundName;
            string DotFillName;
            int AnimationTime;
            if (IsEnabled)
            {
                if (ModMain.DragControl is not null && ModMain.DragControl.Equals(this) || IsMouseOver)
                {
                    ForegroundName = "ColorBrush3";
                    DotFillName = "ColorBrush3";
                    AnimationTime = 40;
                }
                else
                {
                    ForegroundName = "ColorBrushBg0";
                    DotFillName = "ColorBrushBg0";
                    AnimationTime = 100;
                }
            }
            else
            {
                ForegroundName = "ColorBrushGray5";
                DotFillName = "ColorBrushGray5";
                AnimationTime = 200;
            }

            // 触发颜色动画
            if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
            {
                // 有动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaColor(this, BorderBrushProperty, ForegroundName, AnimationTime),
                        ModAnimation.AaColor(ShapeDot, Shape.FillProperty, DotFillName, AnimationTime)
                    }, "MySlider Color " + Uuid);
            }
            else
            {
                // 无动画
                ModAnimation.AniStop("MySlider Color " + Uuid);
                SetResourceReference(BorderBrushProperty, ForegroundName);
                ShapeDot.SetResourceReference(Shape.FillProperty, DotFillName);
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "滑动条颜色改变出错");
        }
    }

    private void MySlider_MouseEnter()
    {
        Focus(); // 确保按键能改变值
    }

    private void MySlider_KeyDown(object sender, KeyEventArgs e)
    {
        // 拒绝一边拖动一边用按键改变
        if (ReferenceEquals(this, ModMain.DragControl))
            return;
        // 改变值
        if (e.Key == Key.Left)
        {
            ChangeByKey = true;
            Value = (int)(Value - ValueByKey);
            ChangeByKey = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            ChangeByKey = true;
            Value = (int)(Value + ValueByKey);
            ChangeByKey = false;
            e.Handled = true;
        }
        else
        {
            return;
        }

        // 更新 Popup
        if (GetHintText is not null)
        {
            RefreshPopup();
            ModAnimation.AniStop("MySlider KeyPopup " + Uuid);
            ModAnimation.AniStart(
                ModAnimation.AaCode(() => Popup.IsOpen = false, (int)Math.Round(700d * ModAnimation.AniSpeed)),
                "MySlider KeyPopup " + Uuid);
        }
    }
}