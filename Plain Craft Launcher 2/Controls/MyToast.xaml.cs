using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

    /// <summary>拖动释放后回到原位的动画时长（毫秒）。</summary>
    private const int ReturnAnimationMs = 150;

    // 拖动状态
    private bool _dragPending;
    private bool _isDragging;
    private Point _dragStartPoint;
    private double _dragStartTranslateX;
    private FrameworkElement? _dragReference;

    // 进度条状态
    private double _progressStartWidth;
    private double _progressTotalMs;

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
            ModAnimation.AniStop($"Toast Show {Uuid}");
            ModAnimation.AniStop($"Toast Hide {Uuid}");
            ModAnimation.AniStop($"Toast Dismiss {Uuid}");
            ModAnimation.AniStop($"Toast Emphasize {Uuid}");
            ModAnimation.AniStop($"Toast Drag Return {Uuid}");
            ProgressBar.BeginAnimation(WidthProperty, null);
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
        Opacity = 0;

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(0, 0, DesiredSize.Width, DesiredSize.Height));
        _targetHeight = Math.Max(ActualHeight, 45d);
        Height = 0;

        RenderTransform = new TranslateTransform(60, 0);

        ModAnimation.AniStop($"Toast Drag Return {Uuid}");
        var enterAnimations = new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, -60, 400, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaHeight(this, _targetHeight, 150, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaOpacity(this, 1, 100)
        };
        ModAnimation.AniStart(enterAnimations, $"Toast Show {Uuid}");

        RestartHideAnimation();
    }

    public void Emphasize()
    {
        ModAnimation.AniStop($"Toast Show {Uuid}");
        ModAnimation.AniStop($"Toast Hide {Uuid}");
        ModAnimation.AniStop($"Toast Emphasize {Uuid}");
        ModAnimation.AniStop($"Toast Drag Return {Uuid}");
        ProgressBar.BeginAnimation(WidthProperty, null);
        if (RenderTransform is TranslateTransform tt) tt.X = 0;
        Opacity = 1;
        Height = _targetHeight;
        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, -8, 70, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaTranslateX(this, 16, 70, after: true),
            ModAnimation.AaTranslateX(this, -8, 60, after: true, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(RestartHideAnimation, after: true),
        }, $"Toast Emphasize {Uuid}");
    }

    private void RestartHideAnimation()
    {
        StartHideAnimation(DisplayDuration);
        StartProgressAnimation(DisplayDuration);
    }

    private void StartHideAnimation(double delayMs)
    {
        var delay = (int)Math.Round(delayMs);
        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, 60, 200, delay, new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaOpacity(this, -1, 150, delay),
            ModAnimation.AaHeight(this, -_targetHeight, 100, ease: new ModAnimation.AniEaseOutFluent(), after: true),
            ModAnimation.AaCode(() =>
            {
                if (Parent is Panel p)
                    p.Children.Remove(this);
            }, after: true)
        }, $"Toast Hide {Uuid}");
    }

    public void Dismiss()
    {
        if (IsDismissing) return;
        IsDismissing = true;
        _isDragging = false;
        _dragPending = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        ModAnimation.AniStop($"Toast Show {Uuid}");
        ModAnimation.AniStop($"Toast Hide {Uuid}");
        ModAnimation.AniStop($"Toast Emphasize {Uuid}");
        ModAnimation.AniStop($"Toast Drag Return {Uuid}");
        ProgressBar.BeginAnimation(WidthProperty, null);
        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, 60, 150, ease: new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaOpacity(this, -1, 100),
            ModAnimation.AaCode(() =>
            {
                if (Parent is Panel p)
                    p.Children.Remove(this);
            }, after: true)
        }, $"Toast Dismiss {Uuid}");
    }

    private void StartProgressAnimation(double duration)
    {
        var totalMs = (int)Math.Round(duration);
        if (totalMs <= 0)
            return;
        var w = ProgressBar.ActualWidth;
        if (w <= 0) w = 300;
        ProgressBar.HorizontalAlignment = HorizontalAlignment.Left;
        ProgressBar.Width = w;
        _progressStartWidth = w;
        _progressTotalMs = totalMs;
        var anim = new DoubleAnimation(w, 0d, TimeSpan.FromMilliseconds(totalMs));
        ProgressBar.BeginAnimation(WidthProperty, anim);
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

        ModAnimation.AniStop($"Toast Show {Uuid}");
        ModAnimation.AniStop($"Toast Hide {Uuid}");
        ModAnimation.AniStop($"Toast Emphasize {Uuid}");
        ModAnimation.AniStop($"Toast Drag Return {Uuid}");

        PauseProgress();

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
        var currentX = (RenderTransform as TranslateTransform)?.X ?? 0d;
        var currentOpacity = Opacity;

        var remaining = GetProgressRemainingMs();
        if (remaining < MinRemainingMs)
        {
            Dismiss();
            return;
        }

        ResumeProgress(remaining);

        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, -currentX, ReturnAnimationMs, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaOpacity(this, 1d - currentOpacity, ReturnAnimationMs),
            ModAnimation.AaCode(() => StartHideAnimation(GetProgressRemainingMs()), after: true)
        }, $"Toast Drag Return {Uuid}");
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

    #region 进度条暂停与恢复

    private void PauseProgress()
    {
        var currentWidth = ProgressBar.Width;
        ProgressBar.BeginAnimation(WidthProperty, null);
        ProgressBar.Width = currentWidth;
    }

    private void ResumeProgress(double remainingMs)
    {
        if (remainingMs <= 0)
            return;
        var currentWidth = ProgressBar.Width;
        if (currentWidth <= 0)
            return;
        var anim = new DoubleAnimation(currentWidth, 0d, TimeSpan.FromMilliseconds(remainingMs));
        ProgressBar.BeginAnimation(WidthProperty, anim);
    }

    private double GetProgressRemainingMs()
    {
        if (_progressStartWidth <= 0)
            return 0d;
        var currentWidth = ProgressBar.Width;
        return _progressTotalMs * (currentWidth / _progressStartWidth);
    }

    #endregion
}
