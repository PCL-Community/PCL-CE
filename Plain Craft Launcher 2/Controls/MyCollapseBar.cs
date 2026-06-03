using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCL;

/// <summary>
/// 轻量折叠栏：一行可点击标题 + 三角，点击切换其下内容区的显示。
/// 无卡片外观（无阴影/边框/背景）、无高度动画，仅切换内容 Visibility 并平滑旋转三角。
/// </summary>
public class MyCollapseBar : StackPanel
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(MyCollapseBar),
            new PropertyMetadata("", (d, e) => ((MyCollapseBar)d)._titleBlock.Text = (string)e.NewValue));

    private readonly int _uuid = ModBase.GetUuid();
    private readonly TextBlock _titleBlock;
    private readonly Path _triangle;
    private readonly StackPanel _contentPanel;
    private bool _isCollapsed;

    public MyCollapseBar()
    {
        Orientation = Orientation.Vertical;

        _titleBlock = new TextBlock
        {
            FontSize = 14d, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(6d, 0d, 0d, 0d), IsHitTestVisible = false
        };
        _titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");

        _triangle = new Path
        {
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform, Height = 6d, Width = 10d, Margin = new Thickness(0d, 0d, 12d, 0d),
            IsHitTestVisible = false,
            Data = (Geometry)new GeometryConverter().ConvertFromString("M2,4 l-2,2 10,10 10,-10 -2,-2 -8,8 -8,-8 z"),
            RenderTransform = new RotateTransform(180d), RenderTransformOrigin = new Point(0.5d, 0.5d)
        };
        _triangle.SetResourceReference(Shape.FillProperty, "ColorBrush1");

        var header = new Grid { Height = 30d, Background = Brushes.Transparent, Cursor = Cursors.Hand };
        header.Children.Add(_titleBlock);
        header.Children.Add(_triangle);
        header.MouseLeftButtonUp += (_, _) => IsCollapsed = !IsCollapsed;

        _contentPanel = new StackPanel { Margin = new Thickness(6d, 2d, 0d, 0d) };

        Children.Add(header);
        Children.Add(_contentPanel);
    }

    /// <summary>标题文本。</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>内容区，调用方向其 Children 添加要折叠的内容。</summary>
    public StackPanel ContentPanel => _contentPanel;

    /// <summary>开合状态实际改变时触发。</summary>
    public event EventHandler? Toggled;

    /// <summary>是否收起。</summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed == value) return;
            _isCollapsed = value;
            _contentPanel.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            var target = value ? 0d : 180d;
            if (IsLoaded)
                ModAnimation.AniStart(
                    ModAnimation.AaRotateTransform(_triangle,
                        target - ((RotateTransform)_triangle.RenderTransform).Angle, 250,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                    "MyCollapseBar " + _uuid, true);
            else
                ((RotateTransform)_triangle.RenderTransform).Angle = target;
            Toggled?.Invoke(this, EventArgs.Empty);
        }
    }
}
