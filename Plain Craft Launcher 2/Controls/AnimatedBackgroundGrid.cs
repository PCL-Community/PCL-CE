using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCL;

public class AnimatedBackgroundGrid : Grid
{
    public static readonly DependencyProperty BackgroundBrushProperty = DependencyProperty.Register(nameof(BackgroundBrush),
        typeof(SolidColorBrush), typeof(AnimatedBackgroundGrid),
        new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), _BackgroundBrushChanged));

    private readonly DependencyProperty _animatableBrushProperty;

    public readonly int uuid = Common.GetUuid();

    public AnimatedBackgroundGrid(DependencyProperty brushDp)
    {
        _animatableBrushProperty = brushDp;
        Loaded += (_, _) => Init();
    }

    public AnimatedBackgroundGrid() : this(BackgroundProperty)
    {
    }

    protected virtual FrameworkElement AnimatableElement => this;

    protected virtual SolidColorBrush AnimatableBrush
    {
        get => (SolidColorBrush)Background;
        set => Background = value;
    }

    protected bool IsAnimating { get; private set; }

    public SolidColorBrush BackgroundBrush
    {
        get => (SolidColorBrush)GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    private static readonly List<(AnimatedBackgroundGrid Grid, SolidColorBrush Brush)> PendingFinishes = [];
    private static DispatcherTimer? _finishTimer;
    private static Dispatcher? _finishTimerDispatcher;

    private static void _BackgroundBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var grid = (AnimatedBackgroundGrid)d;
        var brush = (SolidColorBrush)e.NewValue;

        // 不可见或未加载的实例不参与动画，直接赋值即可
        if (grid is not { IsLoaded: true, IsVisible: true })
        {
            grid.AnimatableBrush = brush;
            return;
        }

        grid.Dispatcher.BeginInvoke(new Action(() =>
        {
            grid.IsAnimating = true;
            Animations.AniStart(
                new[]
                {
                    Animations.AaColor(grid.AnimatableElement, grid._animatableBrushProperty,
                        new Common.MyColor(brush) - grid.AnimatableBrush, 300)
                }, "MyCard Theme " + grid.uuid);
            _ScheduleFinish(grid, brush);
        }));
    }

    private static void _ScheduleFinish(AnimatedBackgroundGrid grid, SolidColorBrush brush)
    {
        // 同一实例重复触发时只保留最新的目标颜色
        for (var i = 0; i < PendingFinishes.Count; i++)
        {
            if (!ReferenceEquals(PendingFinishes[i].Grid, grid)) continue;
            PendingFinishes[i] = (grid, brush);
            _EnsureFinishTimer(grid.Dispatcher);
            return;
        }

        PendingFinishes.Add((grid, brush));
        _EnsureFinishTimer(grid.Dispatcher);
    }

    private static void _EnsureFinishTimer(Dispatcher dispatcher)
    {
        if (_finishTimer is null || !ReferenceEquals(_finishTimerDispatcher, dispatcher))
        {
            _finishTimer?.Stop();
            _finishTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _finishTimer.Tick += _OnFinishTimerTick;
            _finishTimerDispatcher = dispatcher;
        }

        // 一轮主题切换内所有实例几乎同时触发，共用从首次触发开始计时的 300ms 窗口
        if (!_finishTimer.IsEnabled) _finishTimer.Start();
    }

    private static void _OnFinishTimerTick(object? sender, EventArgs e)
    {
        _finishTimer?.Stop();
        if (PendingFinishes.Count == 0) return;

        var pending = PendingFinishes.ToArray();
        PendingFinishes.Clear();

        foreach (var (grid, brush) in pending)
        {
            grid.AnimatableBrush = brush;
            grid.IsAnimating = false;
        }
    }

    private void Init()
    {
        AnimatableBrush = BackgroundBrush;
    }
}
