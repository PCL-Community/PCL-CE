using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace PCL.UI.Controls;

/// <summary>
///     PCL 滚动条。模板见 <c>UI/Styles/Controls/ScrollBars.axaml</c>：滑块底色取自
///     <see cref="TemplatedControl.Foreground" />。悬停时显示，按住时颜色加深。
///     滚轮或拖动引起滚动后，会额外保持约 120 ms 的可见状态再淡出。
///     透明状态下仍保持可命中，以便贴近边缘时直接唤出并抓取滑块。
/// </summary>
public class MyScrollBar : ScrollBar
{
    private const string AccentKey = "ColorBrush3";
    private const string AccentHoverKey = "ColorBrush4";
    private const string ThumbPartName = "PART_Thumb";
    private const int AnimationTime = 90;
    private const double ActiveOpacity = 0.35d;
    private const double PressedOpacity = 0.5d;
    private static readonly TimeSpan ScrollIdleDelay = TimeSpan.FromMilliseconds(120);

    /// <summary>动画组名使用的唯一编号。</summary>
    public readonly int Uuid = LauncherCommon.GetUuid();

    private bool _isCaptureWithin;

    private bool _isScrolling;

    private readonly DispatcherTimer _scrollIdleTimer = new() { Interval = ScrollIdleDelay };

    private Thumb? _thumb;

    public MyScrollBar()
    {
        ContainerTheme.AttachStyles(this);
        IsHitTestVisible = true;
        _scrollIdleTimer.Tick += (_, _) =>
        {
            _scrollIdleTimer.Stop();
            _isScrolling = false;
            RefreshColor();
        };

        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();

        PropertyChanged += OnPropertyChanged;
        ActualThemeVariantChanged += (_, _) => RefreshColor();
        AttachedToVisualTree += (_, _) => RefreshColor();

        RefreshColor();
    }

    /// <summary>按当前指针、拖动与可见状态刷新颜色和透明度。</summary>
    public void RefreshColor()
    {
        try
        {
            var opacity = !IsVisible ? 0d : _isCaptureWithin ? PressedOpacity : IsPointerOver || _isScrolling ? ActiveOpacity : 0d;
            var colorKey = IsPointerOver ? AccentKey : AccentHoverKey;
            var key = "MyScrollBar Color " + Uuid;
            if (!ContainerAnimation.CanAnimate(this))
            {
                AnimationService.CancelAnimationByName(key);
                ContainerTheme.BindDynamic(this, ForegroundProperty, colorKey);
                Opacity = opacity;
                return;
            }

            var delta = ContainerAnimation.ColorOf(Resolve(colorKey)) - ContainerAnimation.ColorOf(Foreground);
            if (_isScrolling)
                Opacity = opacity;
            AnimationService.Run(
                new List<IAnimation>
                {
                    new ColorAnimation(this, ForegroundProperty, delta, AnimationTime),
                    new OpacityAnimation(this, opacity - Opacity, _isScrolling ? 0 : AnimationTime),
                    new ActionAnimation(() => ContainerTheme.BindDynamic(this, ForegroundProperty, colorKey), true)
                },
                key);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "滚动条颜色改变出错");
        }
    }

    public void NotifyScrolled()
    {
        _isScrolling = true;
        _scrollIdleTimer.Stop();
        _scrollIdleTimer.Start();
        RefreshColor();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_thumb is not null)
        {
            _thumb.DragStarted -= OnThumbDragStarted;
            _thumb.DragCompleted -= OnThumbDragCompleted;
            _thumb.PointerCaptureLost -= OnThumbPointerCaptureLost;
        }

        _thumb = e.NameScope.Find<Thumb>(ThumbPartName);

        if (_thumb is not null)
        {
            _thumb.DragStarted += OnThumbDragStarted;
            _thumb.DragCompleted += OnThumbDragCompleted;
            _thumb.PointerCaptureLost += OnThumbPointerCaptureLost;
        }
    }

    private void OnThumbDragStarted(object? sender, VectorEventArgs e)
    {
        _isCaptureWithin = true;
        SetPressedAppearance();
    }

    private void OnThumbDragCompleted(object? sender, VectorEventArgs e)
    {
        EndCapture();
    }

    private void OnThumbPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        EndCapture();
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsEnabledProperty || e.Property == IsVisibleProperty)
            RefreshColor();
    }

    private void EndCapture()
    {
        if (!_isCaptureWithin)
            return;

        _isCaptureWithin = false;
        RefreshColor();
    }

    private void SetPressedAppearance()
    {
        AnimationService.CancelAnimationByName("MyScrollBar Color " + Uuid);
        Opacity = IsVisible ? PressedOpacity : 0d;
    }

    private IBrush Resolve(string key)
    {
        return key == AccentKey ? ContainerTheme.Accent(this) : ContainerTheme.AccentHover(this);
    }
}