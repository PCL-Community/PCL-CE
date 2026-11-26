using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.UI.Theme;
using PCL.UI.Composition;

namespace PCL.Views.Shell;

/// <summary>
/// 主窗口背景入口：供设置页“刷新背景”等外部调用方请求重新加载背景内容。 背景层本身是 <see cref="BackgroundLayer" />
/// ，由主窗口创建并注册到这里。
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

/// <summary>主窗口背景层：按配置绘制颜色主题渐变或随机背景图片，并应用透明度、裁剪模式与真实模糊。 图片使用 <see cref="ImageBrush" /> 支持平铺。
/// 启用背景材质（云母/模糊）时，材质由 <see cref="MaterialPresenter" /> 的原生合成视觉绘制，彩色填充变透明、壁纸隐藏。</summary>
public sealed class BackgroundLayer
{
    private static readonly string[] ImageExtensions =
        [".png", ".jpg", ".jpeg", ".bmp", ".gif"];

    private readonly Panel _formPan;
    private readonly Panel _host;
    private readonly Image _legacyImage;
    private readonly Border _surface;
    private readonly MaterialPresenter _material;
    private readonly MicaMaterial _micaMaterial = new() { Mode = MaterialThemeMode.Auto };
    private readonly MicaMaterial _micaAltMaterial = new() { Mode = MaterialThemeMode.Auto, Variant = MicaVariant.Alt };
    private readonly BlurMaterial _blurMaterial = new();

    private Bitmap? _bitmap;
    private string? _currentPath;
    private ImageBrush? _imageBrush;

    public BackgroundLayer(Panel formPan, Image image, MaterialPresenter material)
    {
        _formPan = formPan;
        _legacyImage = image;
        _legacyImage.IsVisible = false;
        _host = image.Parent as Panel ?? formPan;
        _material = material;

        // 原有 Image 只用于定位宿主；实际背景改用 Border + ImageBrush，以支持平铺与切换动画。
        _surface = new Border
        {
            IsVisible = false,
            IsHitTestVisible = false
        };
        _host.Children.Add(_surface);

        UpdateBlurTint();
        _material.NativeMaterialActiveChanged += RefreshBackdropHandler;
        ThemeService.ColorModeChanged += _OnThemeChanged;
        ThemeService.ColorThemeChanged += _OnThemeChanged;
    }

    private bool IsBackdropActive =>
        Config.Preference.Background.BackdropEffect != BackdropMode.Off && _material.IsNativeMaterialActive;

    public void Refresh()
    {
        RefreshColor();
        RefreshImage();
        ApplyPresentation();
        RefreshBackdrop();
        AnimateContentIn();
    }

    /// <summary>停止当前背景并释放图片句柄，供“清空背景内容”删除文件前调用。</summary>
    public void Clear()
    {
        ClearContent();
    }

    public void RefreshColor()
    {
        try
        {
            _formPan.Background = IsBackdropActive
                ? Brushes.Transparent
                : Config.Preference.Background.BackgroundColorful
                    ? CreateColorfulBrush()
                    : FindThemeBrush("ColorBrushBackground") ?? Brushes.Transparent;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("UI", $"刷新背景颜色失败：{ex.Message}");
        }
    }

    /// <summary>按配置应用背景材质效果：云母（Base/Alt）与模糊都由原生合成层绘制（裁剪在卡片内），
    /// 关闭或合成后端不可用时恢复彩色填充与壁纸。</summary>
    public void RefreshBackdrop()
    {
        var effect = Config.Preference.Background.BackdropEffect;
        // Off 或无效的持久化取值（如损坏的配置）一律视为关闭，与设置页展示保持一致。
        if (effect == BackdropMode.Off || !Enum.IsDefined(effect))
        {
            if (_material.Material is not null)
                _material.Material = null;
            RefreshColor();
            _surface.IsVisible = _bitmap is not null;
            return;
        }

        _material.Material = effect switch
        {
            BackdropMode.Mica => _micaMaterial,
            BackdropMode.MicaAlt => _micaAltMaterial,
            BackdropMode.Blur => _blurMaterial,
            _ => null,
        };
        RefreshColor();
        _surface.IsVisible = _bitmap is not null && !IsBackdropActive;
    }

    public void RefreshImage()
    {
        var pictures = CollectPictures();
        if (pictures.Count == 0)
        {
            ClearContent();
            return;
        }

        foreach (var path in pictures.OrderBy(_ => Random.Shared.Next()))
            if (TryLoadImage(path))
                return;

        ClearContent();
        LogWrapper.Warn("UI", "背景内容目录中没有可用的图片");
    }

    public void ApplyPresentation()
    {
        var opacity = Math.Clamp(
            Config.Preference.Background.WallpaperOpacity / 1000d,
            0d,
            1d);
        _surface.Opacity = opacity;

        ApplySuitMode(Config.Preference.Background.WallpaperSuitMode);

        var blur = Math.Max(Config.Preference.Background.WallpaperBlurRadius, 0);
        var effectRadius = blur > 0 ? blur + 1d : 0d;
        _surface.Effect = effectRadius > 0
            ? new BlurEffect
            {
                Radius = effectRadius
            }
            : null;
        _surface.Margin = blur > 0
            ? new Thickness(-(blur + 1d) / 1.8d)
            : new Thickness(0d);
    }

    public void Remove()
    {
        _material.NativeMaterialActiveChanged -= RefreshBackdropHandler;
        ThemeService.ColorModeChanged -= _OnThemeChanged;
        ThemeService.ColorThemeChanged -= _OnThemeChanged;

        ClearContent();
        _host.Children.Remove(_surface);
        _legacyImage.IsVisible = false;
        _legacyImage.Source = null;
    }

    private bool TryLoadImage(string path)
    {
        if (!File.Exists(path)) return false;

        Bitmap? bitmap = null;
        try
        {
            bitmap = new Bitmap(path);
            _surface.Background = null;
            _imageBrush = null;
            _bitmap?.Dispose();
            _bitmap = bitmap;
            _imageBrush = new ImageBrush(bitmap);
            _surface.Background = _imageBrush;
            _surface.IsVisible = true;
            _currentPath = path;
            ApplySuitMode(Config.Preference.Background.WallpaperSuitMode);
            LogWrapper.Info("UI", $"已加载背景图片：{Path.GetFileName(path)}");
            return true;
        }
        catch (Exception ex)
        {
            _surface.Background = null;
            _imageBrush = null;
            if (ReferenceEquals(_bitmap, bitmap)) _bitmap = null;
            bitmap?.Dispose();
            LogWrapper.Debug(ex, "UI", $"加载背景图片失败：{path}");
            return false;
        }
    }

    private void ClearContent()
    {
        _surface.Background = null;
        _surface.IsVisible = false;
        _imageBrush = null;
        _bitmap?.Dispose();
        _bitmap = null;
        _currentPath = null;
    }

    private void AnimateContentIn()
    {
        if (!_surface.IsVisible) return;

        var opacity = Math.Clamp(
            Config.Preference.Background.WallpaperOpacity / 1000d,
            0d,
            1d);
        _ = AnimationService.FadeAsync(_surface, 0d, opacity, 260);
    }

    private void _OnThemeChanged(bool _, ColorTheme __)
    {
        UpdateBlurTint();
        RefreshBackdrop();
    }

    private void _OnThemeChanged(ColorTheme _)
    {
        UpdateBlurTint();
        RefreshBackdrop();
    }

    private void UpdateBlurTint()
    {
        _blurMaterial.Tint = ThemeService.IsDarkMode ? Color.FromRgb(32, 32, 32) : Color.FromRgb(243, 243, 243);
        _blurMaterial.TintOpacity = ThemeService.IsDarkMode ? 0.72 : 0.55;
    }

    private void RefreshBackdropHandler(object? sender, EventArgs e)
    {
        RefreshBackdrop();
    }

    private void ApplySuitMode(int suitMode)
    {
        if (_imageBrush is null || _bitmap is null) return;

        var brush = _imageBrush;
        var bitmap = _bitmap;

        // 智能模式：小于窗口一半的图片平铺，其余按适应显示。
        if (suitMode == 0) suitMode = IsImageSmall(bitmap) ? 4 : 2;

        brush.TileMode = TileMode.None;
        brush.DestinationRect = RelativeRect.Fill;
        brush.AlignmentX = AlignmentX.Center;
        brush.AlignmentY = AlignmentY.Center;

        switch (suitMode)
        {
            case 1:
                brush.Stretch = Stretch.None;
                break;
            case 2:
                brush.Stretch = Stretch.UniformToFill;
                break;
            case 3:
                brush.Stretch = Stretch.Fill;
                break;
            case 4:
                brush.Stretch = Stretch.None;
                brush.TileMode = TileMode.Tile;
                brush.AlignmentX = AlignmentX.Left;
                brush.AlignmentY = AlignmentY.Top;
                brush.DestinationRect = new RelativeRect(
                    0d,
                    0d,
                    bitmap.Size.Width,
                    bitmap.Size.Height,
                    RelativeUnit.Absolute);
                break;
            case 5:
                brush.Stretch = Stretch.None;
                brush.AlignmentX = AlignmentX.Left;
                brush.AlignmentY = AlignmentY.Top;
                break;
            case 6:
                brush.Stretch = Stretch.None;
                brush.AlignmentX = AlignmentX.Right;
                brush.AlignmentY = AlignmentY.Top;
                break;
            case 7:
                brush.Stretch = Stretch.None;
                brush.AlignmentX = AlignmentX.Left;
                brush.AlignmentY = AlignmentY.Bottom;
                break;
            case 8:
                brush.Stretch = Stretch.None;
                brush.AlignmentX = AlignmentX.Right;
                brush.AlignmentY = AlignmentY.Bottom;
                break;
            default:
                brush.Stretch = Stretch.UniformToFill;
                break;
        }
    }

    private bool IsImageSmall(Bitmap bitmap)
    {
        var window = TopLevel.GetTopLevel(_formPan) as Window;
        var width = window?.Bounds.Width ?? 850d;
        var height = window?.Bounds.Height ?? 500d;
        return bitmap.PixelSize.Width < width / 2d && bitmap.PixelSize.Height < height / 2d;
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
                    .Where(file => ImageExtensions.Contains(
                        Path.GetExtension(file),
                        StringComparer.OrdinalIgnoreCase))
            ];
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("UI", $"枚举背景内容目录失败：{ex.Message}");
            return [];
        }
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