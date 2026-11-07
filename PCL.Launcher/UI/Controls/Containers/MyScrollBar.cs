using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using static PCL.Animations;

namespace PCL.UI.Controls;

/// <summary>
///     PCL 滚动条。模板见 <c>UI/Styles/Controls/ScrollBars.axaml</c>：滑块底色取自
///     <see cref="TemplatedControl.Foreground" />，整体透明度与颜色在悬停（130 ms）、
///     拖动（50 ms）、空闲（180 ms）、隐藏（20 ms）四种状态之间过渡。
/// </summary>
public class MyScrollBar : ScrollBar
{
    private const string AccentKey = "ColorBrush3";
    private const string AccentHoverKey = "ColorBrush4";
    private const string ThumbPartName = "PART_Thumb";

    /// <summary>动画组名使用的唯一编号。</summary>
    public readonly int Uuid = LauncherCommon.GetUuid();

    private string _colorKey = AccentHoverKey;

    private bool _isCaptureWithin; // 指针在滚动条内按下或拖动滑块期间为 true

    private Thumb? _thumb;

    public MyScrollBar()
    {
        ContainerTheme.AttachStyles(this);

        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();

        // 拖动滑块或点击轨道时事件会被子控件取走，这里连同已处理事件一起监听
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble, true);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble, true);

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
            double newOpacity;
            string newColorKey;
            int time;

            if (!IsVisible)
            {
                newOpacity = 0d;
                time = 20; // 防止错误的尺寸判断导致闪烁
                newColorKey = AccentHoverKey;
            }
            else if (_isCaptureWithin)
            {
                newOpacity = 1d;
                time = 50;
                newColorKey = AccentHoverKey;
            }
            else if (IsPointerOver)
            {
                newOpacity = 0.9d;
                time = 130;
                newColorKey = AccentKey;
            }
            else
            {
                newOpacity = 0.5d;
                time = 180;
                newColorKey = AccentHoverKey;
            }

            _colorKey = newColorKey;
            var animationKey = "MyScrollBar Color " + Uuid;

            if (ContainerAnimation.CanAnimate(this))
            {
                // AaColor 的参数是增量而非目标值，必须传“目标 - 当前”，否则颜色会逐次漂移
                var delta = ContainerAnimation.ColorOf(Resolve(newColorKey))
                    - ContainerAnimation.ColorOf(Foreground);

                AniStart(
                    new List<AniData>
                    {
                        AaColor(this, ForegroundProperty, delta, time),
                        AaOpacity(this, newOpacity - Opacity, time),
                        // 收尾恢复动态资源，主题切换后颜色继续跟随页面
                        AaCode(
                            () => ContainerTheme.BindDynamic(this, ForegroundProperty, _colorKey),
                            after: true)
                    },
                    animationKey);
            }
            else
            {
                AniStop(animationKey);
                ContainerTheme.BindDynamic(this, ForegroundProperty, newColorKey);
                Opacity = newOpacity;
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "滚动条颜色改变出错");
        }
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
        RefreshColor();
    }

    private void OnThumbDragCompleted(object? sender, VectorEventArgs e)
    {
        EndCapture();
    }

    private void OnThumbPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        EndCapture();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isCaptureWithin = true;
        RefreshColor();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
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

    private IBrush Resolve(string key)
    {
        return key == AccentKey ? ContainerTheme.Accent(this) : ContainerTheme.AccentHover(this);
    }
}