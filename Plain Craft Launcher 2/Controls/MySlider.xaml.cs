using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

using PCL.Core.App.Localization;
namespace PCL;

public partial class MySlider
{
    public delegate void ChangeEventHandler(object sender, bool user);

    public delegate void PreviewChangeEventHandler(object sender, ModBase.RouteEventArgs e);

    // 自定义属性

    private int _Value;
    private bool changeByKey;

    // 拖动

    public Delegate getHintText;

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
        get => _Value;
        set
        {
            try
            {
                value = (int)Math.Round(ModBase.MathClamp(value, 0d, MaxValue));
                if (_Value == value)
                    return;

                // 触发 Preview 事件，修改新值
                var oldValue = _Value;
                _Value = value;
                if (!ModBase.IsUiUpdating)
                {
                    var e = new ModBase.RouteEventArgs();
                    PreviewChange?.Invoke(this, e);
                    if (e.handled)
                    {
                        _Value = oldValue;
                        DragStop();
                        return;
                    }
                }

                if (IsLoaded && ActualWidth >= ShapeDot.Width)
                    RefreshWidth(null, null);

                if (!ModBase.IsUiUpdating)
                    Change?.Invoke(this, false);
            }

            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    "滑动条进度改变出错",
                    ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Application.Control.Error.OperationFailed"));
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
        var newWidth = _Value / (double)MaxValue * (ActualWidth - ShapeDot.Width);
        LineFore.Width = Math.Max(0d, newWidth + (newWidth < 0.5d ? 0d : 0.5d));
        LineBack.Width = Math.Max(0d,
            ActualWidth - ShapeDot.Width - newWidth + (ActualWidth - ShapeDot.Width - newWidth < 0.5d ? 0d : 0.5d));
        ModBase.SetLeft(ShapeDot, newWidth);
    }

    private void DragStart(object sender, MouseButtonEventArgs e)
    {
        CaptureMouse();
        MouseMove += OnDragMouseMove;
        e.Handled = true; // 防止 ScrollViewer 失焦问题
        ModMain.dragControl = this;
        RefreshColor();
        ModMain.frmMain.DragDoing();
        ApplyDotScale(1.3d);
        RefreshPopup();
        _popupCloseTimer?.Stop();
        _popupCloseTimer = null;
    }

    private DispatcherTimer? _popupCloseTimer;

    private void SchedulePopupClose()
    {
        _popupCloseTimer?.Stop();
        _popupCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _popupCloseTimer.Tick += (_, _) =>
        {
            _popupCloseTimer?.Stop();
            _popupCloseTimer = null;
            Popup.IsOpen = false;
        };
        _popupCloseTimer.Start();
    }

    public void DragDoing()
    {
        var percent =
            ModBase.MathClamp((Mouse.GetPosition(PanMain).X - ShapeDot.Width / 2d) / (ActualWidth - ShapeDot.Width), 0d,
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
        ApplyDotScale(1d);
        Popup.IsOpen = false;
    }

    private void ApplyDotScale(double scale)
    {
        if (ShapeDot.RenderTransform is ScaleTransform transform)
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
        }
    }

    public void RefreshPopup()
    {
        if (getHintText is null)
            return;
        Popup.IsOpen = true;
        TextHint.Text = getHintText.DynamicInvoke(Value)?.ToString() ?? "";
        var typeface = new Typeface(TextHint.FontFamily, TextHint.FontStyle, TextHint.FontWeight, TextHint.FontStretch);
        var formattedText = new FormattedText(TextHint.Text, Thread.CurrentThread.CurrentCulture,
            TextHint.FlowDirection, typeface, TextHint.FontSize, TextHint.Foreground, ModBase.dpi);
        TextHint.Width = formattedText.Width; // 使用手动测量的宽度修复 #1057
    }

    // 指向
    private void RefreshColor()
    {
        try
        {
            // 判断当前颜色
            string foregroundName;
            string dotFillName;
            if (IsEnabled)
            {
                if (ModMain.dragControl is not null && ModMain.dragControl.Equals(this) || IsMouseOver)
                {
                    foregroundName = "ColorBrush3";
                    dotFillName = "ColorBrush3";
                }
                else
                {
                    foregroundName = "ColorBrushBg0";
                    dotFillName = "ColorBrushBg0";
                }
            }
            else
            {
                foregroundName = "ColorBrushGray5";
                dotFillName = "ColorBrushGray5";
            }

            SetResourceReference(BorderBrushProperty, foregroundName);
            ShapeDot.SetResourceReference(Shape.FillProperty, dotFillName);
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
        if (ReferenceEquals(this, ModMain.dragControl))
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
            SchedulePopupClose();
        }
    }
}