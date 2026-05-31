using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL;

public class AnimatedBackgroundGrid : Grid
{
    public static readonly DependencyProperty BackgroundBrushProperty = DependencyProperty.Register("BackgroundBrush",
        typeof(SolidColorBrush), typeof(AnimatedBackgroundGrid),
        new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), _BackgroundBrushChanged));

    private readonly DependencyProperty _animatableBrushProperty;

    public readonly int uuid = ModBase.GetUuid();

    private bool _isAnimating;

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

    protected bool IsAnimating
    {
        get => _isAnimating;
        private set => _isAnimating = value;
    }

    public SolidColorBrush BackgroundBrush
    {
        get => (SolidColorBrush)GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    private static void _BackgroundBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var grid = (AnimatedBackgroundGrid)d;
        var brush = (SolidColorBrush)e.NewValue;
        if (!(grid.IsLoaded && grid.IsVisible))
        {
            grid.AnimatableBrush = brush;
            return;
        }

        grid.Dispatcher.BeginInvoke(new Func<Task>(async () =>
        {
            grid.IsAnimating = true;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(grid.AnimatableElement, grid._animatableBrushProperty,
                        new ModBase.MyColor(brush) - grid.AnimatableBrush, 300)
                }, "MyCard Theme " + grid.uuid);
            await Task.Delay(300);
            grid.AnimatableBrush = brush;
            grid.IsAnimating = false;
        }));
    }

    private void Init()
    {
        AnimatableBrush = BackgroundBrush;
    }
}
