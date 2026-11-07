using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Core.UI.Hints;
using PCL.UI.Controls.Common;
using SvgIconControl = PCL.UI.Controls.SvgIcon.SvgIcon;

namespace PCL.UI.Controls;

/// <summary>
///     右下角浮动提示：按显示时长自动关闭，可向右拖动或点关闭按钮立即移除。
///     同一提示重复出现时抖动强调并重新计时；入场/退场、强调抖动、高度展开与进度条收缩
///     由 <c>PCL.Animations</c> 驱动的五组动画负责。
/// </summary>
public class MyToast : Border
{
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

    private static int _uuidSeed;
    private readonly MyIconButton _btnClose;
    private readonly Rectangle _progressBar;

    private readonly Grid _rootGrid;
    private readonly TextBlock _titleText;
    private readonly SvgIconControl _toastIcon;

    public int Uuid = Interlocked.Increment(ref _uuidSeed);

    // 拖动状态
    private bool _dragPending;
    private IPointer? _dragPointer;
    private Control? _dragReference;
    private Point _dragStartPoint;
    private double _dragStartTranslateX;
    private DateTime _hideDeadline = DateTime.MaxValue;

    private DispatcherTimer? _hideTimer;
    private bool _isDragging;
    private bool _isPointerCaptured;

    // 进度条状态
    private double _progressStartWidth;
    private double _progressTotalMs;

    private double _targetHeight;
    private double _targetWidth;
    private Action? _unwatchTheme;

    public MyToast(HintViewModel hint)
    {
        Hint = hint;
        HorizontalAlignment = HorizontalAlignment.Right;
        CornerRadius = new CornerRadius(8d);
        BorderThickness = new Thickness(0d);
        BoxShadow = new BoxShadows(
            new BoxShadow
            {
                Blur = 16d,
                OffsetY = 4d,
                Color = Color.FromArgb(51, 0, 0, 0)
            });

        _toastIcon = new SvgIconControl
        {
            Width = 20d,
            Height = 20d,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 6d, 0d),
            UseOriginalColor = false
        };
        _titleText = new TextBlock
        {
            Name = "TitleText",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13d,
            FontWeight = FontWeight.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _btnClose = new MyIconButton
        {
            Name = "BtnClose",
            SvgIcon = "lucide/x",
            Theme = MyIconButton.Themes.Custom,
            Width = 20d,
            Height = 20d,
            Margin = new Thickness(8d, 0d, 0d, 0d),
            Focusable = false
        };
        _btnClose.Click += (_, _) => Dismiss();

        var contentGrid = new Grid
        {
            Margin = new Thickness(10d, 8d, 10d, 6d),
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };
        contentGrid.Children.Add(_toastIcon);
        Grid.SetColumn(_titleText, 1);
        contentGrid.Children.Add(_titleText);
        Grid.SetColumn(_btnClose, 2);
        contentGrid.Children.Add(_btnClose);

        _progressBar = new Rectangle
        {
            Name = "ProgressBar",
            Height = 3d,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Bottom,
            RenderTransformOrigin = new RelativePoint(0d, 0.5d, RelativeUnit.Relative),
            RenderTransform = new ScaleTransform(1d, 1d)
        };

        _rootGrid = new Grid
        {
            Name = "RootGrid",
            RowDefinitions = new RowDefinitions("*,3")
        };
        _rootGrid.Children.Add(contentGrid);
        Grid.SetRow(_progressBar, 1);
        _rootGrid.Children.Add(_progressBar);
        _rootGrid.SizeChanged += _OnRootGridSizeChanged;

        Child = _rootGrid;
        FeedbackTheme.AttachStyles(this);

        AddHandler(PointerPressedEvent, Toast_PreviewMouseLeftButtonDown, RoutingStrategies.Tunnel);
        PointerMoved += Toast_PreviewMouseMove;
        PointerReleased += Toast_PreviewMouseLeftButtonUp;
        PointerCaptureLost += Toast_LostMouseCapture;

        Hint.DismissRequested += OnHintDismissRequested;
        Hint.EmphasizeRequested += OnHintEmphasizeRequested;
        Loaded += (_, _) =>
        {
            UpdateColors();
            _unwatchTheme?.Invoke();
            _unwatchTheme = FeedbackTheme.WatchTheme(this, UpdateColors);
        };
        Unloaded += (_, _) =>
        {
            Hint.DismissRequested -= OnHintDismissRequested;
            Hint.EmphasizeRequested -= OnHintEmphasizeRequested;
            _unwatchTheme?.Invoke();
            _unwatchTheme = null;
            _hideTimer?.Stop();
            _hideTimer = null;
            StopAnimations(ShowKey, HideKey, DismissKey, EmphasizeKey, DragReturnKey);
            PCL.Animations.AniStop(ProgressKey);
        };
    }

    /// <summary>本提示控件展示的状态。</summary>
    public HintViewModel Hint { get; }

    public string Context
    {
        get => _titleText.Text ?? string.Empty;
        set => _titleText.Text = value;
    }

    public bool IsDismissing { get; private set; }

    private double DisplayDuration
    {
        get
        {
            var speed = PCL.Animations.AniSpeed;
            if (speed <= 0d) return 0d;
            if (speed >= 200d) return Hint.DisplayDuration;
            return Hint.DisplayDuration * speed;
        }
    }

    private string ShowKey => $"Toast Show {Uuid}";

    private string HideKey => $"Toast Hide {Uuid}";

    private string DismissKey => $"Toast Dismiss {Uuid}";

    private string EmphasizeKey => $"Toast Emphasize {Uuid}";

    private string DragReturnKey => $"Toast Drag Return {Uuid}";

    private string ProgressKey => $"Toast Progress {Uuid}";

    private void OnHintDismissRequested(HintViewModel _)
    {
        Dismiss();
    }

    private void OnHintEmphasizeRequested(HintViewModel _)
    {
        Emphasize();
    }

    #region 显示与关闭

    /// <summary>
    ///     入场：从右侧滑入、高度展开、淡入，同时启动倒计时与进度条收缩。
    /// </summary>
    public void Show()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Show);
            return;
        }

        if (Parent is not Panel)
            return;

        Context = Hint.Message;
        if (TopLevel.GetTopLevel(this) is Window { Bounds.Width: > 0d } window)
            MaxWidth = window.Bounds.Width * 0.9d;
        Margin = new Thickness(0d, 0d, 16d, 4d);

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        _targetHeight = Math.Max(DesiredSize.Height, 45d);
        _targetWidth = Math.Max(DesiredSize.Width, 1d);

        PCL.Animations.AniStop(DragReturnKey);

        if (!FeedbackAnimation.CanAnimate(this))
        {
            Opacity = 1d;
            Height = _targetHeight;
            RenderTransform = new TranslateTransform(0d, 0d);
            RestartHide();
            return;
        }

        Opacity = 0d;
        Height = 0d;
        RenderTransform = new TranslateTransform(60d, 0d);

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaTranslateX(
                    this,
                    -60d,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaHeight(
                    this,
                    _targetHeight,
                    150,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaOpacity(this, 1d, 100)
            },
            ShowKey);

        RestartHide();
    }

    /// <summary>
    ///     强调：复位姿态后左右抖动，并重启倒计时。
    /// </summary>
    public void Emphasize()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Emphasize);
            return;
        }

        if (IsDismissing)
            return;

        StopAnimations(ShowKey, HideKey, EmphasizeKey, DragReturnKey);
        PCL.Animations.AniStop(ProgressKey);
        _hideTimer?.Stop();
        _hideTimer = null;

        var translate = EnsureTranslate();
        translate.X = 0d;
        Opacity = 1d;
        Height = _targetHeight;

        if (!FeedbackAnimation.CanAnimate(this))
        {
            RestartHide();
            return;
        }

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaTranslateX(
                    this,
                    -8d,
                    70,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaTranslateX(this, 16d, 70, after: true),
                PCL.Animations.AaTranslateX(
                    this,
                    -8d,
                    60,
                    after: true,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaCode(RestartHide, after: true)
            },
            EmphasizeKey);
    }

    /// <summary>
    ///     立即关闭：右滑、淡出后移除。
    /// </summary>
    public void Dismiss()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Dismiss);
            return;
        }

        if (IsDismissing)
            return;

        IsDismissing = true;
        _isDragging = false;
        _dragPending = false;
        ReleasePointerCapture();
        _hideTimer?.Stop();
        _hideTimer = null;
        StopAnimations(ShowKey, HideKey, EmphasizeKey, DragReturnKey);
        PCL.Animations.AniStop(ProgressKey);

        if (!FeedbackAnimation.CanAnimate(this))
        {
            RemoveSelf();
            return;
        }

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaTranslateX(
                    this,
                    60d,
                    150,
                    ease: new PCL.Animations.AniEaseInFluent()),
                PCL.Animations.AaOpacity(this, -1d, 100),
                PCL.Animations.AaCode(RemoveSelf, after: true)
            },
            DismissKey);
    }

    /// <summary>
    ///     倒计时结束后的退场：右滑、淡出、高度收拢后移除。
    /// </summary>
    private void BeginHideAnimation()
    {
        if (IsDismissing)
            return;

        IsDismissing = true;
        PauseProgress();

        if (!FeedbackAnimation.CanAnimate(this))
        {
            RemoveSelf();
            return;
        }

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaTranslateX(
                    this,
                    60d,
                    200,
                    ease: new PCL.Animations.AniEaseInFluent()),
                PCL.Animations.AaOpacity(this, -1d, 150),
                PCL.Animations.AaHeight(
                    this,
                    -_targetHeight,
                    100,
                    ease: new PCL.Animations.AniEaseOutFluent(),
                    after: true),
                PCL.Animations.AaCode(RemoveSelf, after: true)
            },
            HideKey);
    }

    private void RemoveSelf()
    {
        if (Parent is Panel panel)
            panel.Children.Remove(this);
        Hint.Close();
    }

    private void StopAnimations(params string[] names)
    {
        foreach (var name in names)
            PCL.Animations.AniStop(name);
    }

    private TranslateTransform EnsureTranslate()
    {
        if (RenderTransform is TranslateTransform translate)
            return translate;

        translate = new TranslateTransform(0d, 0d);
        RenderTransform = translate;
        return translate;
    }

    private void RestartHide()
    {
        StartProgressAnimation(Math.Max(1d, DisplayDuration));
        RestartHideTimer(Math.Max(1d, DisplayDuration));
    }

    private void RestartHideTimer(double durationMs)
    {
        _hideTimer?.Stop();
        _hideDeadline = DateTime.UtcNow.AddMilliseconds(durationMs);
        _hideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(1d, durationMs))
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer?.Stop();
            if (!IsDismissing)
                BeginHideAnimation();
        };
        _hideTimer.Start();
    }

    #region 进度条

    /// <summary>进度条按显示时长线性收缩；动画关闭时直接归零。</summary>
    private void StartProgressAnimation(double durationMs)
    {
        PCL.Animations.AniStop(ProgressKey);
        _progressTotalMs = Math.Max(1d, durationMs);

        if (!FeedbackAnimation.CanAnimate(this))
        {
            _progressBar.Width = 0d;
            return;
        }

        var width = _progressBar.Bounds.Width;
        if (width <= 0d && _targetWidth > 0d)
            width = _targetWidth;
        if (width <= 0d)
            width = 300d;

        _progressStartWidth = width;
        _progressBar.HorizontalAlignment = HorizontalAlignment.Left;
        _progressBar.Width = width;

        PCL.Animations.AniStart(
            PCL.Animations.AaDouble(
                delta => _progressBar.Width = Math.Max(0d, _progressBar.Width + delta),
                -width,
                (int)Math.Round(_progressTotalMs),
                ease: new PCL.Animations.AniEaseLinear()),
            ProgressKey);
    }

    private void PauseProgress()
    {
        PCL.Animations.AniStop(ProgressKey);
    }

    private void ResumeProgress(double remainingMs)
    {
        if (remainingMs <= 0d || !FeedbackAnimation.CanAnimate(this))
            return;

        var current = _progressBar.Width;
        if (double.IsNaN(current) || current <= 0d)
            return;

        PCL.Animations.AniStop(ProgressKey);
        PCL.Animations.AniStart(
            PCL.Animations.AaDouble(
                delta => _progressBar.Width = Math.Max(0d, _progressBar.Width + delta),
                -current,
                (int)Math.Round(remainingMs),
                ease: new PCL.Animations.AniEaseLinear()),
            ProgressKey);
    }

    #endregion

    #endregion

    #region 主题

    private void UpdateColors()
    {
        var baseHue = Hint.Kind switch
        {
            HintKind.Success => 145d,
            HintKind.Error => 355d,
            HintKind.Warning => 40d,
            _ => 210d
        };
        var accent = new SolidColorBrush((Color)new MyColor().FromHsl2(baseHue, 75d, 60d));
        var background = FeedbackTheme.GetBrush(this, "ColorBrushBackground", Brushes.White);
        var text = FeedbackTheme.GetBrush(this, "ColorBrushGray1", Brushes.Black);

        Background = background;
        BorderBrush = background;
        _titleText.Foreground = text;
        _progressBar.Fill = accent;
        _btnClose.Foreground = text;
        _toastIcon.Icon = Hint.Icon;
        _toastIcon.IconBrush = accent;
        _toastIcon.StrokeThickness = 0d;
    }

    private void _OnRootGridSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _rootGrid.Clip = new RectangleGeometry(
            new Rect(0d, 0d, e.NewSize.Width, e.NewSize.Height),
            8d,
            8d);
    }

    #endregion

    #region 拖动关闭

    private void Toast_PreviewMouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    {
        _dragPending = false;
        if (IsDismissing)
            return;
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind
            != PointerUpdateKind.LeftButtonPressed)
            return;
        if (IsDescendantOf(e.Source as Visual, _btnClose))
            return;

        _dragReference = Parent as Control;
        if (_dragReference is null)
            return;

        _dragPending = true;
        _isDragging = false;
        _dragPointer = e.Pointer;
        _dragStartPoint = e.GetPosition(_dragReference);
    }

    private void Toast_PreviewMouseMove(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _dragReference is null)
            {
                _isDragging = false;
                _dragPending = false;
                ReleasePointerCapture();
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
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _dragReference is null)
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

    private void Toast_PreviewMouseLeftButtonUp(object? sender, PointerReleasedEventArgs e)
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
        ReleasePointerCapture();

        var currentX = (RenderTransform as TranslateTransform)?.X ?? 0d;
        if (currentX - _dragStartTranslateX >= GetDismissThreshold())
        {
            Dismiss();
            return;
        }

        ReturnFromDrag();
    }

    private void Toast_LostMouseCapture(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        _dragPending = false;
        _isPointerCaptured = false;
        ReturnFromDrag();
    }

    private void BeginDrag(double initialDelta)
    {
        _isDragging = true;
        _dragPending = false;

        _hideTimer?.Stop();
        _hideTimer = null;
        StopAnimations(ShowKey, HideKey, EmphasizeKey, DragReturnKey);
        PauseProgress();

        Height = _targetHeight;
        _dragStartTranslateX = (RenderTransform as TranslateTransform)?.X ?? 0d;

        if (_dragPointer is not null)
        {
            _dragPointer.Capture(this);
            _isPointerCaptured = true;
        }

        UpdateDragPosition(initialDelta);
    }

    private void UpdateDragPosition(double delta)
    {
        var newX = _dragStartTranslateX + ApplyDragResistance(delta);
        if (RenderTransform is TranslateTransform translate)
            translate.X = newX;
        Opacity = GetDragOpacity(newX);
    }

    /// <summary>
    ///     拖动释放后回位：若剩余显示时间不足则直接关闭，否则恢复进度条并从当前位置动画回原位。
    /// </summary>
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

        var translate = EnsureTranslate();
        var currentX = translate.X;
        var currentOpacity = Opacity;

        ResumeProgress(remaining);
        RestartHideTimer(remaining);

        if (!FeedbackAnimation.CanAnimate(this))
        {
            translate.X = 0d;
            Opacity = 1d;
            return;
        }

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaTranslateX(
                    this,
                    -currentX,
                    ReturnAnimationMs,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                PCL.Animations.AaOpacity(this, 1d - currentOpacity, ReturnAnimationMs)
            },
            DragReturnKey);
    }

    private void ReleasePointerCapture()
    {
        if (!_isPointerCaptured)
            return;

        _dragPointer?.Capture(null);
        _dragPointer = null;
        _isPointerCaptured = false;
    }

    private double GetProgressRemainingMs()
    {
        if (_hideDeadline == DateTime.MaxValue)
            return 0d;

        if (FeedbackAnimation.CanAnimate(this) && _progressStartWidth > 0d && _progressTotalMs > 0d)
        {
            var width = _progressBar.Width;
            if (double.IsNaN(width) || width < 0d)
                width = 0d;
            return _progressTotalMs * Math.Min(1d, width / _progressStartWidth);
        }

        return Math.Max(0d, (_hideDeadline - DateTime.UtcNow).TotalMilliseconds);
    }

    private static double ApplyDragResistance(double delta)
    {
        return Math.Max(0d, delta);
    }

    private double GetDragOpacity(double translateX)
    {
        if (translateX <= 0d)
            return 1d;

        var width = Bounds.Width > 0d ? Bounds.Width : 1d;
        return Math.Max(DragOpacityFloor, 1d - translateX / width * (1d - DragOpacityFloor));
    }

    private double GetDismissThreshold()
    {
        return Math.Max(DismissThresholdMin, Bounds.Width * DismissThresholdRatio);
    }

    private static bool IsDescendantOf(Visual? descendant, Visual ancestor)
    {
        var current = descendant;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
            current = current.GetVisualParent();
        }

        return false;
    }

    #endregion
}