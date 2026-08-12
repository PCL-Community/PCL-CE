using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Theme;
using PCL.Core.Utils;

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
    private bool _pausedByHover;
    private double _hoverRemainingMs;

    /// <summary>悬停暂停开始时刻（毫秒），配合 _hoverRemainingMs 判断暂停期间倒计时是否已走完。</summary>
    private long _pauseStartedAtTick;

    /// <summary>当前隐藏动画倒计时结束、实际开始滑出/淡出的起始 tick（0 表示无待滑出的隐藏）。</summary>
    private long _hideStartsAtTick;

    public MyToast()
    {
        InitializeComponent();
        BtnClose.Click += (_, _) => Dismiss();
        PreviewMouseLeftButtonDown += Toast_PreviewMouseLeftButtonDown;
        PreviewMouseMove += Toast_PreviewMouseMove;
        PreviewMouseLeftButtonUp += Toast_PreviewMouseLeftButtonUp;
        LostMouseCapture += Toast_LostMouseCapture;
        Root.MouseEnter += Root_MouseEnter;
        Root.MouseLeave += Root_MouseLeave;
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
            ModAnimation.AniStop($"Toast StackSettle {Uuid}");
            ProgressBar.BeginAnimation(WidthProperty, null);
            ResetHoverPause();
        };
    }

    private void OnThemeChanged(bool isDarkMode, ColorTheme theme) => UpdateColors();
    private void OnColorThemeChanged(ColorTheme theme) => UpdateColors();

    public string Context
    {
        get => TitleText.Text;
        set => TitleText.Text = value;
    }

    // 点击展开，展示完整内容
    private void ShowDetail()
    {
        if (IsDismissing) return;
        ModMain.MyMsgBox(Context, Lang.Text("Main.Toast.Detail.Title"), Lang.Text("Common.Action.Confirm"));
    }

    public string Icon { get; set; } = "lucide/info";

    public HintType ToastType { get; set; } = HintType.Info;

    public double DisplayDuration { get; set; } = 5000;

    public bool IsDismissing { get; private set; }

    /// <summary>弹窗完全展开后的目标高度，用于纵向排布的定位计算。</summary>
    internal double _targetHeight;

    public void Show()
    {
        if (Parent is not Panel)
            return;
        if (System.Windows.Application.Current.MainWindow is not null)
            MaxWidth = Math.Min(System.Windows.Application.Current.MainWindow.ActualWidth * 0.9, 360d);
        Margin = new Thickness(0, 0, 16, 4);
        Opacity = 0;

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(0, 0, DesiredSize.Width, DesiredSize.Height));
        _targetHeight = Math.Max(ActualHeight, 45d);
        Height = 0;

        RenderTransform = new TranslateTransform(60, 0);

        // 图标“按入”微动效：0.8 → 1，略带过头回弹，呼应启动器按钮的按压语言
        ToastIcon.RenderTransformOrigin = new Point(0.5, 0.5);
        ToastIcon.RenderTransform = new ScaleTransform(0.8, 0.8);

        ModAnimation.AniStop($"Toast Drag Return {Uuid}");
        var enterAnimations = new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, -60, 380, ease: new ModAnimation.AniEaseOutCar()),
            ModAnimation.AaHeight(this, _targetHeight, 150, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaOpacity(this, 1, 100),
            ModAnimation.AaScaleTransform(ToastIcon, 0.2, 260, 120,
                ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak))
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
        _hideStartsAtTick = 0; // 隐藏倒计时被取消，复位标志避免 IsHiding 误判
        ProgressBar.BeginAnimation(WidthProperty, null);
        ResetHoverPause();
        if (RenderTransform is TranslateTransform tt) tt.X = 0;
        Opacity = 1;
        Height = _targetHeight;
        ToastIcon.RenderTransform = new ScaleTransform(1, 1); // 若入场动效尚未结束，先复位图标缩放
        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, -14, 90, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaTranslateX(this, 18, 100, after: true, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaTranslateX(this, -4, 110, after: true, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(RestartHideAnimation, after: true)
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
        _hideStartsAtTick = TimeUtils.GetTimeTick() + delay;
        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, 60, 150, delay, new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaOpacity(this, -1, 110, delay),
            ModAnimation.AaHeight(this, -_targetHeight, 100, ease: new ModAnimation.AniEaseOutFluent(), after: true),
            ModAnimation.AaCode(() =>
            {
                if (Parent is Panel p)
                {
                    p.Children.Remove(this);
                    HintService.OnToastRemoved(this);
                }
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
        ModAnimation.AniStop($"Toast StackSettle {Uuid}");
        _hideStartsAtTick = 0; // 隐藏动画被取消（改为滑动关闭），复位标志避免 IsHiding 误判
        ProgressBar.BeginAnimation(WidthProperty, null);
        ResetHoverPause();
        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, 60, 150, ease: new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaOpacity(this, -1, 100),
            ModAnimation.AaCode(() =>
            {
                if (Parent is Panel p)
                {
                    p.Children.Remove(this);
                    HintService.OnToastRemoved(this);
                }
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
        var isInfo = ToastType == HintType.Info;
        var res = System.Windows.Application.Current.Resources;
        // 信息类型整体用主题色，其余类型用饱和状态色
        var accentBrush = isInfo
            ? (Brush)res["ColorBrush2"]
            : new SolidColorBrush(ToastType switch
            {
                HintType.Success => new ModBase.MyColor().FromHSL2(145d, 75d, 60d),
                HintType.Error => new ModBase.MyColor().FromHSL2(355d, 75d, 60d),
                HintType.Warning => new ModBase.MyColor().FromHSL2(40d, 75d, 60d),
                _ => new ModBase.MyColor().FromHSL2(210d, 75d, 60d)
            });
        var bg = ThemeService.IsDarkMode
            ? new SolidColorBrush(LabColor.FromLch(0.35))
            : (Brush)res["ColorBrushBackground"];
        var text = (SolidColorBrush)res["ColorBrushGray1"];
        var track = new SolidColorBrush(ThemeService.IsDarkMode
            ? Color.FromArgb(70, 255, 255, 255) // 暗色卡片上略亮的内嵌暗槽
            : Color.FromArgb(60, 0, 0, 0));      // 亮色卡片上略暗的内嵌暗槽

        // 卡片背景移到 RootGrid：模糊 RootGrid 时背景一起模糊，并被 8px 圆角裁剪；阴影仍在 Root 上不受影响
        RootGrid.Background = bg;
        TitleText.Foreground = text;
        ProgressTrack.Fill = track;
        ProgressBar.Fill = accentBrush; // Info 为主题色，其余类型为状态色
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
            ShowDetail();
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
        _hideStartsAtTick = 0; // 拖拽取消隐藏倒计时，复位标志避免 IsHiding 误判

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
            ModAnimation.AaCode(() =>
            {
                HintService.RearrangeToasts(); // 复位后重排，让位/补位的旧弹窗回到正确位置
                StartHideAnimation(GetProgressRemainingMs());
            }, after: true)
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

    private void Root_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_isDragging || _pausedByHover || IsDismissing)
            return;
        // 隐藏动画已进入滑出/淡出阶段：不悬停暂停，避免停掉半途的隐藏导致卡半透明、永不消失
        if (_hideStartsAtTick > 0 && TimeUtils.GetTimeTick() >= _hideStartsAtTick)
            return;
        // 悬停暂停：停住隐藏倒计时并冻结进度条，移出后按剩余时间恢复
        ModAnimation.AniStop($"Toast Hide {Uuid}");
        var currentWidth = ProgressBar.Width;
        ProgressBar.BeginAnimation(WidthProperty, null);
        ProgressBar.Width = currentWidth;
        _hoverRemainingMs = GetProgressRemainingMs();
        _pauseStartedAtTick = TimeUtils.GetTimeTick();
        _hideStartsAtTick = 0; // 悬停暂停即取消隐藏倒计时，结束判定改由 _pauseStartedAtTick + 剩余时长得出
        _pausedByHover = true;
        ProgressBar.Opacity = 0.4;
    }

    private void Root_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDragging)
            return; // 拖拽接管进度条，悬停恢复交由 ReturnFromDrag 处理
        if (!_pausedByHover)
            return;
        _pausedByHover = false;
        ProgressBar.Opacity = 1;
        if (_hoverRemainingMs <= 0 || ProgressBar.Width <= 0)
            return; // 已结束或未启动，无需恢复动画
        // 异常路径：暂停期间倒计时已走完，不再恢复倒计时，直接滑出消失
        if (_pauseStartedAtTick > 0 && TimeUtils.GetTimeTick() - _pauseStartedAtTick >= _hoverRemainingMs)
        {
            StartHideAnimation(0);
            return;
        }
        StartHideAnimation(_hoverRemainingMs);
        var currentWidth = ProgressBar.Width;
        var anim = new DoubleAnimation(currentWidth, 0d, TimeSpan.FromMilliseconds(_hoverRemainingMs));
        ProgressBar.BeginAnimation(WidthProperty, anim);
    }

    private void ResetHoverPause()
    {
        if (!_pausedByHover)
            return;
        _pausedByHover = false;
        _hoverRemainingMs = 0;
        _pauseStartedAtTick = 0;
        ProgressBar.Opacity = 1;
    }

    #endregion
}
