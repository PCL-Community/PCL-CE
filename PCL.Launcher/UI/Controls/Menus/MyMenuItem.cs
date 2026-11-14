using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.Logging;
using Path = Avalonia.Controls.Shapes.Path;

namespace PCL.UI.Controls;

/// <summary>
///     带图标的菜单项：<see cref="SvgIcon" /> 优先，为空时回退到旧版 Path 图标（<see cref="MenuItem.Icon" />
///     为 SVG path 字符串或 <see cref="Geometry" />）；悬停/禁用时用核心颜色键做颜色过渡，
///     点击触发 <see cref="CustomEventService" />，并在加载时按窗口透明度设置所在 ContextMenu 的不透明度。
/// </summary>
public class MyMenuItem : MenuItem
{
    private const string Module = "UI.MyMenuItem";

    private const int AnimationTimeIn = 100;
    private const int AnimationTimeOut = 200;

    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyMenuItem, string>(nameof(SvgIcon), string.Empty);

    private string? _colorKey;

    static MyMenuItem()
    {
        SvgIconProperty.Changed.AddClassHandler<MyMenuItem>((item, _) => item.UpdateIcon());
        IsEnabledProperty.Changed.AddClassHandler<MyMenuItem>((item, _) => item.RefreshColor());
    }

    public MyMenuItem()
    {
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        Click += (_, _) => CustomEventService.Raise(this);
        Loaded += _OnLoaded;
    }

    public int Uuid { get; } = Guid.NewGuid().GetHashCode();

    public string SvgIcon
    {
        get => GetValue(SvgIconProperty);
        set => SetValue(SvgIconProperty, value);
    }

    private string ColorAnimationKey => "MyMenuItem Color " + Uuid;

    private void _OnLoaded(object? sender, RoutedEventArgs e)
    {
        UpdateIcon();
        ApplyContextMenuOpacity();

        Background ??= Brushes.Transparent;
        RefreshColor();
    }

    private void UpdateIcon()
    {
        if (!string.IsNullOrWhiteSpace(SvgIcon))
        {
            Icon = new SvgIcon.SvgIcon
            {
                Icon = SvgIcon,
                Width = 16d,
                Height = 16d,
                VerticalAlignment = VerticalAlignment.Center
            };
            return;
        }

        Icon = Icon switch
        {
            null => null,
            Geometry geometry => CreateLegacyIcon(geometry),
            string text when !string.IsNullOrWhiteSpace(text) => CreateLegacyIcon(text),
            _ => Icon
        };
    }

    private Control? CreateLegacyIcon(string pathData)
    {
        try
        {
            return CreateLegacyIcon(Geometry.Parse(pathData));
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, Module, "菜单项旧版 Path 图标解析失败");
            return null;
        }
    }

    private Control CreateLegacyIcon(Geometry geometry)
    {
        var path = new Path
        {
            Data = geometry,
            Width = 16d,
            Height = 16d,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        path.Bind(
            Shape.FillProperty,
            new Binding(nameof(Foreground))
            {
                Source = this
            });
        return path;
    }

    private void RefreshColor()
    {
        var (backKey, foreKey, time) = GetVisualState();
        if (_colorKey == backKey) return;
        _colorKey = backKey;

        var animate = IsLoaded
            && !AnimationService.IsSuspended
            && AnimationService.Speed > 0d
            && ResourceExists(backKey)
            && ResourceExists(foreKey);

        if (animate)
        {
            AnimationService.Run(
                new List<IAnimation>
                {
                    new ColorAnimation(this, BackgroundProperty, backKey, time),
                    new ColorAnimation(this, ForegroundProperty, foreKey, time)
                },
                ColorAnimationKey);
            return;
        }

        AnimationService.CancelAnimationByName(ColorAnimationKey);
        SetDynamicResource(BackgroundProperty, backKey);
        SetDynamicResource(ForegroundProperty, foreKey);
    }

    private (string Background, string Foreground, int Time) GetVisualState()
    {
        return !IsEnabled ? ("ColorBrushTransparent", "ColorBrushGray5", AnimationTimeOut) :
            IsPointerOver ? ("ColorBrush6", "ColorBrush2", AnimationTimeIn) :
            ("ColorBrushTransparent", "ColorBrush1", AnimationTimeOut);
    }

    private static bool ResourceExists(string key)
    {
        return Application.Current is { } application
            && application.TryFindResource(key, out _);
    }

    private void SetDynamicResource(AvaloniaProperty property, string key)
    {
        if (ResourceExists(key))
            Bind(property, new DynamicResourceExtension(key));
        else
            ClearValue(property);
    }

    private void ApplyContextMenuOpacity()
    {
        if (!ConfigService.IsInitialized) return;

        var menu = this.GetVisualAncestors().OfType<ContextMenu>().FirstOrDefault()
            ?? this.FindLogicalAncestorOfType<ContextMenu>();

        menu?.Opacity = Math.Clamp(
            Config.Preference.Theme.WindowOpacity / 1000d + 0.4d,
            0d,
            1d);
    }
}