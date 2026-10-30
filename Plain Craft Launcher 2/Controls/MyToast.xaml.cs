using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.UI.Theme;

namespace PCL;

public partial class MyToast
{
    public int Uuid = ModBase.GetUuid();

    /// <summary>判定为拖动而非点击的最小水平位移（像素）。</summary>
    private const double DragDeadzone = 4d;

    /// <summary>拖动时透明度下限，确保控件始终可见。</summary>
    private const double DragOpacityFloor = 0.35d;

    /// <summary>触发关闭的位移占控件宽度的比例。</summary>
    private const double DismissThresholdRatio = 0.12d;

    /// <summary>触发关闭的最小绝对位移（像素）。</summary>
    private const double DismissThresholdMin = 24d;

    /// <summary>拖动释放后，若剩余显示时间不足此值则直接关闭。</summary>
    private const double MinRemainingMs = 300d;

    // 拖动状态
    private bool _dragPending;
    private bool _isDragging;
    private Point _dragStartPoint;
    private double _dragStartTranslateX;
    private FrameworkElement? _dragReference;

    // 自动关闭计时
    private DispatcherTimer? _hideTimer;
    private long _hideStartTick;
    private double _hideDurationMs;
    private double _pausedRemainingMs = -1d;

    public MyToast()
    {
        InitializeComponent();
        BtnClose.Click += (_, _) => Dismiss();
        PreviewMouseLeftButtonDown += Toast_PreviewMouseLeftButtonDown;
        PreviewMouseMove += Toast_PreviewMouseMove;
        PreviewMouseLeftButtonUp += Toast_PreviewMouseLeftButtonUp;
        LostMouseCapture += Toast_LostMouseCapture;
        Loaded += (_, _) =>
        {
            UpdateColors();
            ThemeService.ColorModeChanged += OnThemeChanged;
            ThemeService.ColorThemeChanged += OnColorThemeChanged;
        };
        Unloaded += (_, _) =>
        {
            ThemeService.ColorModeChanged -= OnThemeChanged;
            ThemeService.ColorThemeChanged -= OnColorThemeChanged;
            StopHideTimer();
        };
    }

    private void OnThemeChanged(bool isDarkMode, ColorTheme theme) => UpdateColors();
    private void OnColorThemeChanged(ColorTheme theme) => UpdateColors();

    public string Context
    {
        get => TitleText.Text;
        set => TitleText.Text = value;
    }

    public string Icon { get; set; } = "lucide/info";

    public HintType ToastType { get; set; } = HintType.Info;

    public double DisplayDuration { get; set; } = 5000;

    public bool IsDismissing { get; private set; }

    private double _targetHeight;

    public void Show()
    {
        if (Parent is not Panel)
            return;
        if (System.Windows.Application.Current.MainWindow is not null)
            MaxWidth = System.Windows.Application.Current.MainWindow.ActualWidth * 0.9;
        Margin = new Thickness(0, 0, 16, 4);

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(0, 0, DesiredSize.Width, DesiredSize.Height));
        _targetHeight = Math.Max(ActualHeight, 45d);

        Opacity = 1d;
        Height = _targetHeight;
        RenderTransform = new TranslateTransform(0d, 0d);

        RestartHideTimer();
    }

    public void Emphasize()
    {
        StopHideTimer();
        if (RenderTransform is TranslateTransform tt) tt.X = 0;
        Opacity = 1;
        Height = _targetHeight;
        RestartHideTimer();
    }

    private void RestartHideTimer()
    {
        StartHideTimer(DisplayDuration);
    }

    private void StartHideTimer(double delayMs)
    {
        StopHideTimer();
        _pausedRemainingMs = -1d;
        var ms = Math.Max(0d, delayMs);
        _hideDurationMs = ms;
        _hideStartTick = Stopwatch.GetTimestamp();
        if (ms <= 0d)
        {
            RemoveSelf();
            return;
        }

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        _hideTimer.Tick += (_, _) =>
        {
            StopHideTimer();
            RemoveSelf();
        };
        _hideTimer.Start();
    }

    private void StopHideTimer()
    {
        _hideTimer?.Stop();
        _hideTimer = null;
    }

    private void RemoveSelf()
    {
        if (Parent is Panel panel)
            panel.Children.Remove(this);
    }

    public void Dismiss()
    {
        if (IsDismissing) return;
        IsDismissing = true;
        _isDragging = false;
        _dragPending = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        StopHideTimer();
        RemoveSelf();
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RootGrid.Clip = new RectangleGeometry(new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight), 8, 8);
    }

    private void UpdateColors()
    {
        var baseHue = ToastType switch
        {
            HintType.Success => 145d,
            HintType.Error => 355d,
            HintType.Warning => 40d,
            _ => 210d
        };
        var res = System.Windows.Application.Current.Resources;
        var accent = new ModBase.MyColor().FromHSL2(baseHue, 75, 60);
        var bg = ThemeService.IsDarkMode
            ? new SolidColorBrush(LabColor.FromLch(0.35))
            : (Brush)res["ColorBrushBackground"];
        var text = (SolidColorBrush)res["ColorBrushGray1"];
        var accentBrush = new SolidColorBrush(accent);

        Root.Background = bg;
        Root.BorderBrush = bg;
        TitleText.Foreground = text;
        ProgressBar.Fill = accentBrush;
        BtnClose.Foreground = text;
        ToastIcon.Icon = Icon;
        ToastIcon.IconBrush = accentBrush;
        ToastIcon.StrokeThickness = 0;
    }

    #region 拖动关闭

    private void Toast_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragPending = false;
        if (IsDismissing)
            return;
        if (IsDescendantOf(e.OriginalSource as DependencyObject, BtnClose))
            return;
        _dragReference = Parent as FrameworkElement;
        if (_dragReference is null)
            return;
        _dragPending = true;
        _isDragging = false;
        _dragStartPoint = e.GetPosition(_dragReference);
    }

    private void Toast_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed || _dragReference is null)
            {
                _isDragging = false;
                _dragPending = false;
                if (IsMouseCaptured) ReleaseMouseCapture();
                ReturnFromDrag();
                return;
            }
            var dragCurrent = e.GetPosition(_dragReference);
            UpdateDragPosition(dragCurrent.X - _dragStartPoint.X);
            e.Handled = true;
            return;
        }

        if (!_dragPending)
            return;
        if (Mouse.LeftButton != MouseButtonState.Pressed || _dragReference is null)
        {
            _dragPending = false;
            return;
        }

        var current = e.GetPosition(_dragReference);
        var delta = current.X - _dragStartPoint.X;

        if (delta < DragDeadzone)
            return;

        BeginDrag(delta);
    }

    private void Toast_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragPending && !_isDragging)
        {
            _dragPending = false;
            return;
        }

        if (!_isDragging)
            return;

        _isDragging = false;
        _dragPending = false;
        e.Handled = true;

        if (IsMouseCaptured)
            ReleaseMouseCapture();

        var currentX = (RenderTransform as TranslateTransform)?.X ?? 0d;
        if (currentX - _dragStartTranslateX >= GetDismissThreshold())
        {
            Dismiss();
            return;
        }

        ReturnFromDrag();
    }

    private void Toast_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
            return;
        _isDragging = false;
        _dragPending = false;
        ReturnFromDrag();
    }

    private void BeginDrag(double initialDelta)
    {
        _isDragging = true;
        _dragPending = false;

        PauseHideTimer();

        Height = _targetHeight;
        _dragStartTranslateX = (RenderTransform as TranslateTransform)?.X ?? 0d;

        CaptureMouse();

        UpdateDragPosition(initialDelta);
    }

    private void UpdateDragPosition(double delta)
    {
        var newX = _dragStartTranslateX + ApplyDragResistance(delta);
        if (RenderTransform is TranslateTransform tt)
            tt.X = newX;
        Opacity = GetDragOpacity(newX);
    }

    private void ReturnFromDrag()
    {
        if (Parent is null || IsDismissing)
            return;

        var remaining = GetProgressRemainingMs();
        if (remaining < MinRemainingMs)
        {
            Dismiss();
            return;
        }

        if (RenderTransform is TranslateTransform tt)
            tt.X = 0d;
        Opacity = 1d;
        ResumeHideTimer(remaining);
    }

    private static double ApplyDragResistance(double delta)
    {
        return Math.Max(0d, delta);
    }

    private double GetDragOpacity(double translateX)
    {
        if (translateX <= 0d)
            return 1d;
        var width = ActualWidth > 0 ? ActualWidth : 1d;
        return Math.Max(DragOpacityFloor, 1d - (translateX / width) * (1d - DragOpacityFloor));
    }

    private double GetDismissThreshold()
    {
        return Math.Max(DismissThresholdMin, ActualWidth * DismissThresholdRatio);
    }

    private static bool IsDescendantOf(DependencyObject? descendant, DependencyObject ancestor)
    {
        while (descendant is not null)
        {
            if (ReferenceEquals(descendant, ancestor))
                return true;
            descendant = VisualTreeHelper.GetParent(descendant);
        }
        return false;
    }

    #endregion

    #region 自动关闭计时暂停与恢复

    private void PauseHideTimer()
    {
        if (_hideTimer is not null)
            _pausedRemainingMs = GetProgressRemainingMs();
        StopHideTimer();
    }

    private void ResumeHideTimer(double remainingMs)
    {
        if (remainingMs <= 0)
            return;
        StartHideTimer(remainingMs);
    }

    private double GetProgressRemainingMs()
    {
        if (_hideTimer is null)
            return Math.Max(0d, _pausedRemainingMs);
        var elapsed = (Stopwatch.GetTimestamp() - _hideStartTick) * 1000d / Stopwatch.Frequency;
        return Math.Max(0d, _hideDurationMs - elapsed);
    }

    #endregion
}
