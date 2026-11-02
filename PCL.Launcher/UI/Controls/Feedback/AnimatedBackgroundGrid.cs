using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MyColor = PCL.UI.Controls.Common.MyColor;

namespace PCL.UI.Controls;

/// <summary>
///     背景色可平滑过渡的面板。
///     主题切换时通过 <c>PCL.Animations.AaColor</c> 在 300ms 内过渡背景色；
///     动画关闭（<c>Config.Debug.AnimationSpeed = 0</c>）时直接赋终值。
/// </summary>
public class AnimatedBackgroundGrid : Grid
{
    public static readonly StyledProperty<SolidColorBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<AnimatedBackgroundGrid, SolidColorBrush?>(
            nameof(BackgroundBrush),
            new SolidColorBrush(Colors.Transparent));

    private static int _uuidSeed;

    private readonly AvaloniaProperty _animatableBrushProperty;

    public readonly int uuid = Interlocked.Increment(ref _uuidSeed);

    public AnimatedBackgroundGrid(AvaloniaProperty brushProperty)
    {
        _animatableBrushProperty = brushProperty;
        FeedbackTheme.AttachStyles(this);
        AttachedToVisualTree += (_, _) => Init();
        Unloaded += (_, _) =>
        {
            PCL.Animations.AniStop(AnimationKey);
            IsAnimating = false;
        };
    }

    public AnimatedBackgroundGrid() : this(BackgroundProperty)
    {
    }

    /// <summary>
    ///     实际参与颜色过渡的元素。派生类可以指向内部承载背景的子元素。
    /// </summary>
    protected virtual Control AnimatableElement => this;

    /// <summary>
    ///     实际参与颜色过渡的画刷。
    /// </summary>
    protected virtual SolidColorBrush? AnimatableBrush
    {
        get => Background as SolidColorBrush;
        set => Background = value;
    }

    /// <summary>颜色过渡是否正在进行。</summary>
    protected new bool IsAnimating { get; private set; }

    public SolidColorBrush? BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    private string AnimationKey => FeedbackAnimation.Name("MyCard Theme", uuid);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BackgroundBrushProperty)
            OnBackgroundBrushChanged(change.NewValue as SolidColorBrush);
    }

    private void OnBackgroundBrushChanged(SolidColorBrush? brush)
    {
        if (brush is null)
        {
            AnimatableBrush = null;
            return;
        }

        if (!IsLoaded || !IsVisible || !FeedbackAnimation.CanAnimate(this))
        {
            AnimatableBrush = brush;
            return;
        }

        var target = new MyColor(brush);
        var current = AnimatableBrush is { } currentBrush
            ? new MyColor(currentBrush)
            : new MyColor(0d, 0d, 0d, 0d);

        IsAnimating = true;
        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaColor(
                    AnimatableElement,
                    _animatableBrushProperty,
                    target - current,
                    300),
                PCL.Animations.AaCode(
                    () =>
                    {
                        AnimatableBrush = brush;
                        IsAnimating = false;
                    },
                    after: true)
            },
            AnimationKey);
    }

    private void Init()
    {
        AnimatableBrush = BackgroundBrush;
    }
}