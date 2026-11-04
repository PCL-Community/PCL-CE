using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.UI.Theme;

namespace PCL.Views.Shell;

/// <summary>
///     主窗口背景入口：供设置页“刷新背景”等外部调用方请求重新加载背景内容。
///     背景层本身是 <see cref="BackgroundLayer" />，由主窗口创建并注册到这里。
/// </summary>
public static class BackgroundHost
{
    public static BackgroundLayer? Current { get; private set; }

    public static void RequestRefresh()
    {
        Current?.Refresh();
    }

    internal static void Attach(BackgroundLayer layer)
    {
        Current = layer;
    }

    internal static void Detach(BackgroundLayer layer)
    {
        if (ReferenceEquals(Current, layer)) Current = null;
    }
}

/// <summary>
///     主窗口背景层：按配置绘制颜色主题渐变或随机背景图片，并应用透明度与裁剪模式。
///     视频背景在 Avalonia 端没有等价播放器，本期降级为日志。
/// </summary>
public sealed class BackgroundLayer
{
    private static readonly string[] ImageExtensions =
        [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff"];

    private readonly Panel _formPan;
    private readonly Image _image;
    private Bitmap? _bitmap;

    public BackgroundLayer(Panel formPan, Image image)
    {
        _formPan = formPan;
        _image = image;
        ThemeService.ColorModeChanged += _OnThemeChanged;
        ThemeService.ColorThemeChanged += _OnThemeChanged;
    }

    public void Refresh()
    {
        RefreshColor();
        RefreshImage();
        ApplyPresentation();
    }

    public void RefreshColor()
    {
        try
        {
            _formPan.Background = Config.Preference.Background.BackgroundColorful
                ? CreateColorfulBrush()
                : FindThemeBrush("ColorBrushBackground") ?? Brushes.Transparent;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("UI", $"刷新背景颜色失败：{ex.Message}");
        }
    }

    public void RefreshImage()
    {
        var pictures = CollectPictures();
        if (pictures.Count == 0)
        {
            ClearImage();
            return;
        }

        var path = pictures[Random.Shared.Next(pictures.Count)];
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (!ImageExtensions.Contains(extension))
        {
            // TODO(SHL-03): WPF 使用 MediaElement 播放视频背景，Avalonia 当前没有等价播放器，降级为日志并跳过
            LogWrapper.Info("UI", $"背景内容 {Path.GetFileName(path)} 不是图片；视频背景本期未实现，已跳过");
            ClearImage();
            return;
        }

        try
        {
            var bitmap = new Bitmap(path);
            _bitmap?.Dispose();
            _bitmap = bitmap;
            _image.Source = bitmap;
            _image.IsVisible = true;
            LogWrapper.Info("UI", $"已加载背景图片：{Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            // TODO(SHL-03): 图片加载失败时 WPF 会回退到视频背景，Avalonia 端无播放器，降级为日志
            LogWrapper.Warn(ex, "UI", $"加载背景图片失败，视频背景本期降级：{path}");
            ClearImage();
        }
    }

    public void ApplyPresentation()
    {
        _image.Opacity = Math.Clamp(Config.Preference.Background.WallpaperOpacity / 1000d, 0d, 1d);
        ApplySuitMode(Config.Preference.Background.WallpaperSuitMode);

        var blur = Config.Preference.Background.WallpaperBlurRadius;
        if (blur > 0)
            // TODO(SHL-03): Avalonia 12 没有可用的背景模糊效果，降级为无模糊并记录配置值
            LogWrapper.Info("UI", $"背景模糊（{blur}）在当前界面框架没有等价实现，已降级为无模糊");
    }

    public void Remove()
    {
        ThemeService.ColorModeChanged -= _OnThemeChanged;
        ThemeService.ColorThemeChanged -= _OnThemeChanged;
        _bitmap?.Dispose();
        _bitmap = null;
        _image.Source = null;
    }

    private void _OnThemeChanged(bool _, ColorTheme __)
    {
        RefreshColor();
    }

    private void _OnThemeChanged(ColorTheme _)
    {
        RefreshColor();
    }

    private void ClearImage()
    {
        _image.Source = null;
        _image.IsVisible = false;
    }

    private static List<string> CollectPictures()
    {
        var directory = Path.Combine(Paths.Data, "Pictures");
        try
        {
            Directory.CreateDirectory(directory);
            return
            [
                .. Directory
                    .EnumerateFiles(directory)
                    .Where(file => !file.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)
                        && !file.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            ];
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("UI", $"枚举背景内容目录失败：{ex.Message}");
            return [];
        }
    }

    private void ApplySuitMode(int suitMode)
    {
        var isAuto = suitMode == 0;
        if (isAuto)
            suitMode = _bitmap is not null && IsImageSmall(_bitmap) ? 3 : 2;

        _image.Width = double.NaN;
        _image.Height = double.NaN;
        _image.HorizontalAlignment = HorizontalAlignment.Center;
        _image.VerticalAlignment = VerticalAlignment.Center;

        switch (suitMode)
        {
            case 1:
            case 5:
            case 6:
            case 7:
            case 8:
            {
                if (_bitmap is not null)
                {
                    _image.Width = _bitmap.PixelSize.Width;
                    _image.Height = _bitmap.PixelSize.Height;
                }

                _image.Stretch = Stretch.None;
                _image.HorizontalAlignment = suitMode switch
                {
                    1 => HorizontalAlignment.Center,
                    5 or 7 => HorizontalAlignment.Left,
                    _ => HorizontalAlignment.Right
                };
                _image.VerticalAlignment = suitMode switch
                {
                    1 => VerticalAlignment.Center,
                    5 or 6 => VerticalAlignment.Top,
                    _ => VerticalAlignment.Bottom
                };
                break;
            }
            case 3:
            {
                _image.Stretch = Stretch.Fill;
                _image.HorizontalAlignment = HorizontalAlignment.Stretch;
                _image.VerticalAlignment = VerticalAlignment.Stretch;
                break;
            }
            case 4:
            {
                // TODO(SHL-03): WPF 的平铺模式使用 ImageBrush.TileMode，Avalonia Image 无平铺属性，暂用拉伸近似
                _image.Stretch = Stretch.UniformToFill;
                _image.HorizontalAlignment = HorizontalAlignment.Stretch;
                _image.VerticalAlignment = VerticalAlignment.Stretch;
                break;
            }
            default:
            {
                _image.Stretch = Stretch.UniformToFill;
                _image.HorizontalAlignment = HorizontalAlignment.Stretch;
                _image.VerticalAlignment = VerticalAlignment.Stretch;
                break;
            }
        }
    }

    private static bool IsImageSmall(Bitmap bitmap)
    {
        var window = MainWindow.Current;
        var width = window?.Bounds.Width ?? 850d;
        var height = window?.Bounds.Height ?? 500d;
        return bitmap.PixelSize.Width < width / 2d && bitmap.PixelSize.Height < height / 2d;
    }

    private static LinearGradientBrush CreateColorfulBrush()
    {
        var tone = ThemeService.CurrentTone;
        var (hue, _, _) = ThemeService.GetCurrentThemeArgs();
        var darkLight = ThemeService.IsDarkMode ? 0.2d : 1d;
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.9d, 0d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.1d, 1d, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(
            new GradientStop(
                ToColor(LabColor.FromLch(0.84d * darkLight, tone.C5, hue - 15d)),
                -0.1d));
        brush.GradientStops.Add(
            new GradientStop(ToColor(LabColor.FromLch(0.96d * darkLight, tone.C7, hue)), 0.4d));
        brush.GradientStops.Add(
            new GradientStop(
                ToColor(LabColor.FromLch(0.84d * darkLight, tone.C5, hue + 15d)),
                1.1d));
        return brush;
    }

    private static Color ToColor(LabColor lab)
    {
        var (alpha, red, green, blue) = lab.ToScRgb();
        return Color.FromArgb(SrgbByte(alpha), SrgbByte(red), SrgbByte(green), SrgbByte(blue));
    }

    private static byte SrgbByte(double linear)
    {
        var encoded = linear <= 0.0031308d
            ? 12.92d * linear
            : 1.055d * Math.Pow(linear, 1d / 2.4d) - 0.055d;
        return (byte)Math.Clamp(Math.Round(encoded * 255d), 0d, 255d);
    }

    private static IBrush? FindThemeBrush(string key)
    {
        if (Application.Current is { } app && app.TryFindResource(key, out var resource))
            return resource as IBrush;
        return null;
    }
}