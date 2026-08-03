using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.App;
using PCL.Core.UI.Controls.SvgIcon;

namespace PCL;

public class MyMenuItem : MenuItem
{
    // 指向动画

    private const int AnimationTimeIn = 100;
    private const int AnimationTimeOut = 200;

    public static readonly DependencyProperty SvgIconProperty = DependencyProperty.Register(
        nameof(SvgIcon),
        typeof(string),
        typeof(MyMenuItem),
        new PropertyMetadata(string.Empty, OnSvgIconChanged));

    private SvgIcon? _svgIconControl;
    private string _colorName;

    // 基础

    public int Uuid = ModBase.GetUuid();

    public MyMenuItem()
    {
        Loaded += MyMenuItem_Loaded;
        MouseEnter += (_, _) => RefreshColor();
        MouseLeave += (_, _) => RefreshColor();
        IsEnabledChanged += (_, _) => RefreshColor();
    }

    public string SvgIcon
    {
        get => (string)GetValue(SvgIconProperty);
        set => SetValue(SvgIconProperty, value);
    }

    private static void OnSvgIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MyMenuItem { IsLoaded: true } item)
            item.UpdateTemplateIcon();
    }

    private (string BackName, string ForeName, int Time) GetVisualState()
    {
        if (!IsEnabled)
            return ("ColorBrushTransparent", "ColorBrushGray5", AnimationTimeOut);
        if (IsMouseOver)
            return ("ColorBrush6", "ColorBrush2", AnimationTimeIn);
        return ("ColorBrushTransparent", "ColorBrush1", AnimationTimeOut);
    }

    private void MyMenuItem_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateTemplateIcon();
        ((ContextMenu)Parent).Opacity = Config.Preference.Theme.WindowOpacity / 1000.0 + 0.4;
    }

    private void UpdateTemplateIcon()
    {
        var iconControl = (Path)GetTemplateChild("Icon");
        if (iconControl is null)
            return;

        if (SvgIconControlHelper.HasSvgIcon(SvgIcon))
        {
            iconControl.Visibility = Visibility.Collapsed;
            EnsureSvgIconControl(iconControl);
            _svgIconControl!.Icon = SvgIcon;
            _svgIconControl.Visibility = Visibility.Visible;
            return;
        }

        _svgIconControl?.Visibility = Visibility.Collapsed;
        if (Icon is null) return;

        iconControl.Visibility = Visibility.Visible;
        iconControl.Data = (Geometry)new GeometryConverter().ConvertFromString(Icon.ToString());
    }

    private void EnsureSvgIconControl(Path iconControl)
    {
        if (_svgIconControl is not null)
            return;

        _svgIconControl = new SvgIcon
        {
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform,
            Margin = iconControl.Margin,
            Height = iconControl.Height,
            Width = iconControl.Width,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        _svgIconControl.SetBinding(Core.UI.Controls.SvgIcon.SvgIcon.IconBrushProperty,
            new Binding(nameof(Foreground)) { Source = this });

        if (VisualTreeHelper.GetParent(iconControl) is not Grid grid) return;

        Grid.SetColumn(_svgIconControl, Grid.GetColumn(iconControl));
        Grid.SetRow(_svgIconControl, Grid.GetRow(iconControl));
        grid.Children.Add(_svgIconControl);
    }

    private void RefreshColor()
    {
        var (backName, foreName, time) = GetVisualState();

        // 重复性验证
        if ((_colorName ?? "") == (backName ?? ""))
            return;
        _colorName = backName;
        // 触发颜色动画
        if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
        {
            // 有动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, BackgroundProperty, backName, time),
                    ModAnimation.AaColor(this, ForegroundProperty, foreName, time)
                }, "MyMenuItem Color " + Uuid);
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("MyMenuItem Color " + Uuid);
            SetResourceReference(BackgroundProperty, backName);
            SetResourceReference(ForegroundProperty, foreName);
        }
    }

    private void MyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ModMain.RaiseCustomEvent(this);
    }
}