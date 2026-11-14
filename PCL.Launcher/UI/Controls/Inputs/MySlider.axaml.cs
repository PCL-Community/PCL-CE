using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.UI.Controls.Common;

namespace PCL.UI.Controls;

/// <summary>
///     整数滑动条：双导轨 + 拖点 + 拖动数值提示。
///     保留 <see cref="Value" />/<see cref="MaxValue" />/<see cref="ValueByKey" />、
///     <see cref="Change" />/<see cref="PreviewChange" /> 事件、<c>getHintText</c> 提示委托与
///     键盘左右方向键步进。进度、配色与拖点缩放经 新动画引擎 过渡。
/// </summary>
public partial class MySlider : UserControl
{
    public delegate void ChangeEventHandler(object sender, bool user);

    public delegate void PreviewChangeEventHandler(object sender, RouteEventArgs e);

    // 数值提示淡入/淡出时长（毫秒）。
    private const int PopupFadeInTime = 100;
    private const int PopupFadeOutTime = 80;

    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<MySlider, int>(
            nameof(Value),
            defaultBindingMode: BindingMode.TwoWay);

    // 基础

    public int Uuid = InputTheme.NewUuid();

    private bool _changeByKey;
    private bool _isDragging;

    // 提示 Popup

    private bool _isPopupClosing;
    private bool _isUpdatingValue;
    private Point _lastPointerPosition;
    private DispatcherTimer? _popupCloseTimer;

    // 拖动提示

    /// <summary>
    ///     值提示委托：接收当前值，返回要显示的文本。
    /// </summary>
    public Delegate? getHintText;

    public MySlider()
    {
        InitializeComponent();

        // 取得焦点时不带着外层滚动容器滚动：拖动与悬停都会触发 Focus
        ScrollViewer.SetBringIntoViewOnFocusChange(this, false);

        SizeChanged += (_, _) => RefreshLayout();
        Loaded += (_, _) => RefreshLayout();
        Unloaded += (_, _) => StopAnimations();
    }

    public int MaxValue
    {
        get;
        set
        {
            if (value == field)
                return;
            field = value;
            RefreshLayout();
        }
    } = 100;

    public int Value
    {
        get => GetValue(ValueProperty);
        set
        {
            var clamped = (int)Math.Round(MathHelper.MathClamp(value, 0d, MaxValue));
            if (clamped == Value)
                return;
            SetCurrentValue(ValueProperty, clamped);
        }
    }

    /// <summary>
    ///     键盘左右方向键的步进量。
    /// </summary>
    public uint ValueByKey { get; set; } = 1U;

    private string ProgressKey => InputAnimation.Key("MySlider Progress", Uuid);
    private string ForegroundKey => InputAnimation.Key("MySlider Foreground", Uuid);
    private string DotFillKey => InputAnimation.Key("MySlider DotFill", Uuid);
    private string DotStrokeKey => InputAnimation.Key("MySlider DotStroke", Uuid);
    private string ScaleKey => InputAnimation.Key("MySlider Scale", Uuid);
    private string PopupFadeKey => InputAnimation.Key("MySlider Popup", Uuid);

    // 布局与颜色

    private double DotWidth => ShapeDotElement.Width;

    public event ChangeEventHandler? Change;

    public event PreviewChangeEventHandler? PreviewChange;

    private void StopAnimations()
    {
        InputAnimation.Stop(ProgressKey, ForegroundKey, DotFillKey, DotStrokeKey, ScaleKey);
        AnimationService.CancelAnimationByName(PopupFadeKey);
        _isPopupClosing = false;

        // 离树时收起提示，避免浮层残留在窗口上
        if (PopupElement is not null && PopupElement.IsOpen)
            PopupElement.IsOpen = false;
    }

    private void OnValueChanged(int oldValue, int newValue)
    {
        if (_isUpdatingValue)
            return;

        try
        {
            var value = (int)Math.Round(MathHelper.MathClamp(newValue, 0d, MaxValue));
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

            // 触发 Preview 事件，可拦截本次修改
            if (!AnimationService.IsSuspended)
            {
                var args = new RouteEventArgs();
                PreviewChange?.Invoke(this, args);
                if (args.handled)
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

            AnimateProgress();

            if (!AnimationService.IsSuspended)
                Change?.Invoke(this, false);
        }
        catch (Exception ex)
        {
            InputTheme.LogDebug(ex, "滑动条进度改变出错");
        }
    }

    private double GetDotPosition()
    {
        var trackWidth = Bounds.Width - DotWidth;
        if (trackWidth <= 0d || MaxValue <= 0)
            return 0d;

        var percent = MathHelper.MathClamp(Value / (double)MaxValue, 0d, 1d);
        return percent * trackWidth;
    }

    /// <summary>
    ///     立即把导轨与拖点写到当前值的布局位置，不播放动画。
    /// </summary>
    private void RefreshLayout()
    {
        if (LineForeElement is null || LineBackElement is null || ShapeDotElement is null)
            return;

        var width = Bounds.Width;
        if (width <= 0d)
            return;

        AnimationService.CancelAnimationByName(ProgressKey);

        var dotPosition = GetDotPosition();
        var backWidth = Math.Max(0d, width - DotWidth - dotPosition);

        LineForeElement.Width = Math.Max(0d, dotPosition + (dotPosition < 0.5d ? 0d : 0.5d));
        LineBackElement.Width = backWidth + (backWidth < 0.5d ? 0d : 0.5d);
        ShapeDotElement.Margin = new Thickness(dotPosition, 0d, 0d, 0d);
        PopupElement.HorizontalOffset = dotPosition + 5d;
    }

    /// <summary>
    ///     把进度过渡到当前值；布局未就绪或动画关闭时立即落到终值。
    /// </summary>
    private void AnimateProgress()
    {
        if (LineForeElement is null || LineBackElement is null || ShapeDotElement is null)
            return;

        var width = Bounds.Width;
        if (width <= DotWidth || MaxValue <= 0 || !InputAnimation.CanAnimate(this))
        {
            RefreshLayout();
            return;
        }

        var trackWidth = width - DotWidth;
        var newWidth = Value / (double)MaxValue * trackWidth;
        var deltaProcess = Math.Abs(LineForeElement.Width / trackWidth - Value / (double)MaxValue);
        var time = (int)Math.Round(
            (1d - Math.Pow(1d - deltaProcess, 3d)) * 300d + (_changeByKey ? 100d : 0d));

        IEasing ease = time > 50
            ? new FluentEaseOut()
            : new LinearEasing();

        var foreWidth = Math.Max(0d, newWidth + (newWidth < 0.5d ? 0d : 0.5d));
        var backWidth = Math.Max(
            0d,
            trackWidth - newWidth + (trackWidth - newWidth < 0.5d ? 0d : 0.5d));

        AnimationService.Run(
            new List<IAnimation>
            {
                new WidthAnimation(
                    LineForeElement,
                    foreWidth - LineForeElement.Width,
                    time,
                    ease: ease),
                new WidthAnimation(
                    LineBackElement,
                    backWidth - LineBackElement.Width,
                    time,
                    ease: ease),
                new XAnimation(
                    ShapeDotElement,
                    newWidth - ShapeDotElement.Margin.Left,
                    time,
                    ease: ease)
            },
            ProgressKey);

        PopupElement.HorizontalOffset = newWidth + 5d;
    }

    /// <summary>
    ///     按拖动/悬停/禁用状态更新导轨与拖点配色。
    /// </summary>
    private void RefreshColor(bool animate = true)
    {
        if (ShapeDotElement is null || LineForeElement is null || LineBackElement is null)
            return;

        string key;
        IBrush fallback;
        int time;

        if (!IsEnabled)
        {
            key = "ColorBrushGray5";
            fallback = InputTheme.Brush(this, key, Brushes.LightGray);
            time = 200;
        }
        else if (_isDragging || IsPointerOver)
        {
            key = InputTheme.AccentKey;
            fallback = InputTheme.Accent(this);
            time = 40;
        }
        else
        {
            key = InputTheme.IdleBorderKey;
            fallback = InputTheme.IdleBorder(this);
            time = 100;
        }

        InputAnimation.Color(
            LineForeElement,
            Border.BackgroundProperty,
            key,
            fallback,
            time,
            ForegroundKey,
            animate);
        InputAnimation.Color(
            ShapeDotElement,
            Shape.FillProperty,
            key,
            fallback,
            time,
            DotFillKey,
            animate);
        InputAnimation.Color(
            ShapeDotElement,
            Shape.StrokeProperty,
            key,
            fallback,
            time,
            DotStrokeKey,
            animate);

        LineBackElement.Background = InputTheme.Brush(
            this,
            InputTheme.IdleBorderKey,
            Brushes.LightSteelBlue);
        if (HintBorderElement is not null)
        {
            HintBorderElement.BorderBrush = InputTheme.Text(this);
            HintBorderElement.Background = InputTheme.Card(this);
        }

        TextHintElement?.Foreground = InputTheme.Text(this);
    }

    // 拖动

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isDragging = true;
        _lastPointerPosition = e.GetPosition(PanMainElement);
        e.Pointer.Capture(this);
        e.Handled = true;

        Focus(); // 确保按键能改变值
        RefreshColor();
        if (DialogPresenter.Instance.HasOpenDialog)
            return;
        InputAnimation.Scale(
            ShapeDotElement,
            1.3d,
            40,
            ScaleKey,
            ease: new FluentEaseOut());
        UpdateValueFromPosition(_lastPointerPosition);
        RefreshPopup();
        StopPopupCloseTimer();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isDragging)
            return;

        _lastPointerPosition = e.GetPosition(PanMainElement);
        if (DialogPresenter.Instance.HasOpenDialog)
        {
            DragStop();
            return;
        }

        UpdateValueFromPosition(_lastPointerPosition);
        RefreshPopup();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isDragging || e.InitialPressMouseButton != MouseButton.Left)
            return;

        DragStop();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        if (_isDragging)
            DragStop();
    }

    /// <summary>
    ///     按当前指针位置更新值。
    /// </summary>
    public void DragDoing()
    {
        if (DialogPresenter.Instance.HasOpenDialog)
        {
            DragStop();
            return;
        }

        UpdateValueFromPosition(_lastPointerPosition);
        RefreshPopup();
    }

    /// <summary>
    ///     结束拖动：恢复拖点缩放、配色并关闭提示。
    /// </summary>
    public void DragStop()
    {
        _isDragging = false;
        RefreshColor();
        InputAnimation.Scale(
            ShapeDotElement,
            1d,
            200,
            ScaleKey,
            ease: new FluentEaseOut());
        ClosePopup();
        StopPopupCloseTimer();
    }

    private void UpdateValueFromPosition(Point position)
    {
        var width = Bounds.Width;
        if (width <= DotWidth || MaxValue <= 0)
            return;

        var percent = MathHelper.MathClamp(
            (position.X - DotWidth / 2d) / (width - DotWidth),
            0d,
            1d);
        var newValue = (int)Math.Round(percent * MaxValue);
        if (newValue != Value)
            Value = newValue;
    }

    /// <summary>
    ///     刷新拖动时跟随的数值提示，首次显示时淡入。
    ///     Popup 的内容挂在独立浮层上，Popup 控件自身不在内容的视觉父级链上，
    ///     给 Popup 设 <c>Opacity</c> 不会影响实际显示，因此透明度动画作用在承载内容的 Border 上。
    /// </summary>
    public void RefreshPopup()
    {
        if (DialogPresenter.Instance.HasOpenDialog)
            return;
        if (getHintText is null || PopupElement is null || TextHintElement is null)
            return;

        TextHintElement.Text = getHintText.DynamicInvoke(Value)?.ToString() ?? string.Empty;

        if (PopupElement.IsOpen)
        {
            // 淡出过程中又需要提示时，取消淡出并把内容恢复到不透明
            if (!_isPopupClosing)
                return;

            _isPopupClosing = false;
            if (HintBorderElement is not null)
                AnimationService.Run(
                    new OpacityAnimation(HintBorderElement, 1d, PopupFadeInTime),
                    PopupFadeKey);
            return;
        }

        _isPopupClosing = false;
        if (HintBorderElement is null || !InputAnimation.CanAnimate(this))
        {
            if (HintBorderElement is not null)
                HintBorderElement.Opacity = 1d;
            PopupElement.IsOpen = true;
            return;
        }

        HintBorderElement.Opacity = 0d;
        PopupElement.IsOpen = true;
        AnimationService.Run(
            new OpacityAnimation(
                HintBorderElement,
                1d,
                PopupFadeInTime,
                ease: new FluentEaseOut()),
            PopupFadeKey);
    }

    /// <summary>
    ///     关闭数值提示：先淡出承载内容的 Border，动画结束后再关闭 Popup；
    ///     动画关闭或内容已透明时直接关闭。
    /// </summary>
    private void ClosePopup()
    {
        if (PopupElement is null || !PopupElement.IsOpen)
            return;

        if (HintBorderElement is null
            || !InputAnimation.CanAnimate(this)
            || HintBorderElement.Opacity <= 0.01d)
        {
            AnimationService.CancelAnimationByName(PopupFadeKey);
            PopupElement.IsOpen = false;
            return;
        }

        _isPopupClosing = true;
        AnimationService.CancelAnimationByName(PopupFadeKey);
        AnimationService.Run(
            new List<IAnimation>
            {
                new OpacityAnimation(
                    HintBorderElement,
                    -HintBorderElement.Opacity,
                    PopupFadeOutTime,
                    ease: new FluentEaseOut()),
                new ActionAnimation(
                    () =>
                    {
                        _isPopupClosing = false;
                        PopupElement.IsOpen = false;
                        HintBorderElement.Opacity = 1d;
                    },
                    true)
            },
            PopupFadeKey);
    }

    private void StartPopupCloseTimer()
    {
        _popupCloseTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700d)
        };
        _popupCloseTimer.Tick -= PopupCloseTimer_Tick;
        _popupCloseTimer.Tick += PopupCloseTimer_Tick;
        _popupCloseTimer.Stop();
        _popupCloseTimer.Start();
    }

    private void StopPopupCloseTimer()
    {
        _popupCloseTimer?.Stop();
    }

    private void PopupCloseTimer_Tick(object? sender, EventArgs e)
    {
        StopPopupCloseTimer();
        ClosePopup();
    }

    // 键盘

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // 拒绝一边拖动一边用按键改变
        if (_isDragging)
            return;

        var step = Math.Max(1, (int)ValueByKey);
        int? target = e.Key switch
        {
            Key.Left or Key.Down => Value - step,
            Key.Right or Key.Up => Value + step,
            Key.PageDown => Value - step * 10,
            Key.PageUp => Value + step * 10,
            Key.Home => 0,
            Key.End => MaxValue,
            _ => null
        };
        if (target is null)
            return;

        _changeByKey = true;
        try
        {
            Value = Math.Clamp(target.Value, 0, MaxValue);
        }
        finally
        {
            _changeByKey = false;
        }

        e.Handled = true;

        // 更新 Popup
        if (getHintText is not null)
        {
            RefreshPopup();
            StartPopupCloseTimer();
        }
    }

    // 属性变更处理

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
            OnValueChanged(
                change.OldValue is int oldValue ? oldValue : 0,
                change.NewValue is int newValue ? newValue : 0);
        else if (change.Property == IsEnabledProperty)
            RefreshColor();
    }

    // 主题

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        PopupElement.PlacementTarget = this;
        PopupElement.Placement = PlacementMode.Bottom;

        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        RefreshColor(false);
        RefreshLayout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        PointerEntered -= OnPointerEntered;
        PointerExited -= OnPointerExited;
        StopAnimations();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        RefreshColor();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        RefreshColor();
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        RefreshColor(false);
    }
}