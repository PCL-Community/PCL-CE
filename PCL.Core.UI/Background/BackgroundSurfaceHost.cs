using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.UI.Animation.Core;
using PCL.Core.UI.Media;

namespace PCL.Core.UI.Background;

/// <summary>背景视图的尺寸和缩放信息。</summary>
public readonly record struct BackgroundViewport(
    double Width,
    double Height,
    double Scaling);

/// <summary>承载背景内容的主机区域。</summary>
public sealed class BackgroundSurfaceHost
{
    private readonly Panel _host;
    private readonly Image _legacyImage;
    private readonly Border _surface;

    public BackgroundSurfaceHost(Panel formPan, Image legacyImage)
    {
        _legacyImage = legacyImage;
        _legacyImage.IsVisible = false;
        _host = legacyImage.Parent as Panel ?? formPan;
        _surface = new Border
        {
            IsVisible = false,
            IsHitTestVisible = false
        };
        _host.Children.Add(_surface);
    }

    public Rect HostBounds => _host.Bounds;

    public bool HasContent =>
        _surface.IsVisible
        && (_surface.Child is not null || _surface.Background is not null);

    public void SetVideo(Control? view)
    {
        if (view is null)
        {
            Clear();
            return;
        }

        _surface.Background = null;
        _surface.Child = view;
        _surface.IsVisible = true;
    }

    public void SetImage(ImageBrush? brush)
    {
        if (brush is null)
        {
            Clear();
            return;
        }

        _surface.Child = null;
        _surface.Background = brush;
        _surface.IsVisible = true;
    }

    public void Clear()
    {
        _surface.Child = null;
        _surface.Background = null;
        _surface.IsVisible = false;
    }

    public void ApplyPresentation(VideoSurface? video, double opacity, double blur)
    {
        _surface.Opacity = opacity;

        var shaderBlur = video?.SupportsShaderBlur == true;
        video?.BlurRadius = shaderBlur ? blur : 0d;

        var effectRadius = blur > 0d && !shaderBlur ? blur + 1d : 0d;
        _surface.Effect = effectRadius > 0d
            ? new BlurEffect
            {
                Radius = effectRadius
            }
            : null;
        _surface.Margin = effectRadius > 0d
            ? new Thickness(-effectRadius / 1.8d)
            : new Thickness(0d);
    }

    public void AnimateIn(double opacity)
    {
        if (!_surface.IsVisible) return;
        _ = AnimationService.FadeAsync(
            _surface,
            0d,
            opacity,
            260);
    }

    public void Remove()
    {
        Clear();
        _host.Children.Remove(_surface);
        _legacyImage.IsVisible = false;
        _legacyImage.Source = null;
    }
}