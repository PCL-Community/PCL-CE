using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;
using static PCL.Animations;

namespace PCL.UI.Controls;

/// <summary>
///     轻量折叠栏：一行可点击标题 + 三角，点击切换内容区显示。
///     高度展开/收起与箭头旋转由 <c>PCL.Animations</c> 驱动，快速连点时先停旧动画再量新高度；
///     开合期间会临时关闭父卡片的动画，避免两套高度动画互相覆盖。
/// </summary>
public class MyCollapseBar : StackPanel
{
    private const double HeaderHeight = 30d;

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MyCollapseBar, string>(nameof(Title), string.Empty);

    private readonly TextBlock _titleBlock;
    private readonly Path _triangle;
    private (MyCard card, bool useAnimation)? _parentCardState;

    static MyCollapseBar()
    {
        TitleProperty.Changed.AddClassHandler<MyCollapseBar>((bar, e) =>
            bar._titleBlock.Text = e.NewValue as string ?? string.Empty);
    }

    public MyCollapseBar()
    {
        ContainerTheme.AttachStyles(this);

        Orientation = Orientation.Vertical;
        ClipToBounds = true;

        _titleBlock = new TextBlock
        {
            FontSize = 14d,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(6d, 0d, 0d, 0d),
            IsHitTestVisible = false
        };
        _titleBlock.Classes.Add("CollapseTitle");

        _triangle = new Path
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform,
            Height = 6d,
            Width = 10d,
            Margin = new Thickness(0d, 0d, 12d, 0d),
            IsHitTestVisible = false,
            Data = Geometry.Parse("M2,4 l-2,2 10,10 10,-10 -2,-2 -8,8 -8,-8 z"),
            RenderTransform = new RotateTransform(180d),
            RenderTransformOrigin = RelativePoint.Center
        };
        _triangle.Classes.Add("CollapseTriangle");

        var header = new Grid
        {
            Height = HeaderHeight,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        header.Children.Add(_titleBlock);
        header.Children.Add(_triangle);
        header.PointerReleased += (_, _) => IsCollapsed = !IsCollapsed;

        ContentPanel = new StackPanel
        {
            Margin = new Thickness(6d, 2d, 0d, 0d)
        };

        Children.Add(header);
        Children.Add(ContentPanel);
    }

    /// <summary>标题文本。</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>内容区，调用方向其 Children 添加要折叠的内容。</summary>
    public StackPanel ContentPanel { get; }

    /// <summary>是否收起。</summary>
    public bool IsCollapsed
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;

            var rotate = ContainerAnimation.EnsureRotate(_triangle, 180d);
            var targetAngle = value ? 0d : 180d;
            var rotateKey = "MyCollapseBar " + _uuid;

            if (ContainerAnimation.CanAnimate(this))
            {
                AniStart(
                    AaRotateTransform(
                        _triangle,
                        targetAngle - rotate.Angle,
                        250,
                        ease: new AniEaseOutFluent(AniEasePower.ExtraStrong)),
                    rotateKey,
                    true);
            }
            else
            {
                AniStop(rotateKey);
                rotate.Angle = targetAngle;
            }

            if (ContainerAnimation.CanAnimate(this) && Bounds.Height > 0d)
            {
                if (value)
                    CollapseWithAnimation();
                else
                    ExpandWithAnimation();
            }
            else
            {
                // 未加载或动画关闭：直接显隐，并把可能残留的高度还原
                AniStop(HeightAnimationKey);
                RestoreParentCardOnInterrupt();
                Height = double.NaN;
                ContentPanel.IsVisible = !value;
            }

            Toggled?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>开合状态实际改变时触发。</summary>
    public event EventHandler? Toggled;

    /// <summary>若上一次折叠动画被中断且未恢复，则先恢复父卡片的动画开关。</summary>
    private void RestoreParentCardOnInterrupt()
    {
        if (_parentCardState is { } state)
        {
            state.card.UseAnimation = state.useAnimation;
            _parentCardState = null;
        }
    }

    private void SilenceParentCard()
    {
        if (_parentCardState is not null)
            return;

        for (var current = Parent; current is not null; current = current.Parent)
            if (current is MyCard card)
            {
                _parentCardState = (card, card.UseAnimation);
                card.UseAnimation = false;
                return;
            }
    }

    private void RestoreParentCard()
    {
        if (_parentCardState is { } state)
        {
            state.card.UseAnimation = state.useAnimation;
            _parentCardState = null;
        }
    }

    #region 高度展开/收起

    private readonly int _uuid = LauncherCommon.GetUuid();

    private string HeightAnimationKey => "MyCollapseBar Height " + _uuid;

    private void CollapseWithAnimation()
    {
        AniStop(HeightAnimationKey);
        RestoreParentCardOnInterrupt();
        SilenceParentCard();

        var fullHeight = Bounds.Height;
        Height = fullHeight;

        AniStart(
            new List<AniData>
            {
                AaHeight(
                    this,
                    HeaderHeight - fullHeight,
                    200,
                    ease: new AniEaseOutFluent(AniEasePower.ExtraStrong)),
                AaCode(
                    () =>
                    {
                        ContentPanel.IsVisible = false;
                        Height = double.NaN;
                        RestoreParentCard();
                    },
                    after: true)
            },
            HeightAnimationKey);
    }

    private void ExpandWithAnimation()
    {
        AniStop(HeightAnimationKey);
        RestoreParentCardOnInterrupt();
        SilenceParentCard();

        ContentPanel.IsVisible = true;
        Height = double.NaN;
        // 直接量内容区，避免对自身 Measure 的缓存判断：展开后的自然高度 = 标题 + 内容 + 上边距
        var contentWidth = Bounds.Width > 0d
            ? Math.Max(0d, Bounds.Width - ContentPanel.Margin.Left - ContentPanel.Margin.Right)
            : double.PositiveInfinity;
        ContentPanel.Measure(new Size(contentWidth, double.PositiveInfinity));
        var fullHeight = Math.Max(
            HeaderHeight,
            HeaderHeight + ContentPanel.Margin.Top + ContentPanel.DesiredSize.Height);
        Height = HeaderHeight;

        AniStart(
            new List<AniData>
            {
                AaHeight(
                    this,
                    fullHeight - HeaderHeight,
                    200,
                    ease: new AniEaseOutFluent(AniEasePower.ExtraStrong)),
                AaCode(
                    () =>
                    {
                        Height = double.NaN;
                        RestoreParentCard();
                    },
                    after: true)
            },
            HeightAnimationKey);
    }

    #endregion
}