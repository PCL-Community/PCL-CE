using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.Logging;

namespace PCL.Core.UI.Background;

/// <summary>负责背景图片位图、画刷和适配模式。</summary>
public sealed class BackgroundImageController(
    Func<BackgroundViewport> viewportProvider) : IDisposable
{
    private Bitmap? _bitmap;
    private bool _disposed;

    public ImageBrush? Brush { get; private set; }

    public bool HasImage => _bitmap is not null;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeContent();
    }

    public event Action? Changed;

    public bool TryLoad(string path)
    {
        if (_disposed || !File.Exists(path)) return false;

        Bitmap? bitmap = null;
        try
        {
            bitmap = new Bitmap(path);
            var brush = new ImageBrush(bitmap);
            DisposeContent();
            _bitmap = bitmap;
            Brush = brush;
        }
        catch (Exception ex)
        {
            bitmap?.Dispose();
            LogWrapper.Debug(ex, "UI", $"加载背景图片失败：{path}");
            return false;
        }

        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "UI", "应用背景图片状态失败");
        }

        LogWrapper.Info("UI", $"已加载背景图片：{Path.GetFileName(path)}");
        return true;
    }

    public void Clear()
    {
        if (_disposed) return;
        DisposeContent();
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "UI", "清除背景图片状态失败");
        }
    }

    public void ApplySuitMode(int suitMode)
    {
        if (Brush is null || _bitmap is null) return;

        var brush = Brush;
        var bitmap = _bitmap;

        if (suitMode == 0)
        {
            var viewport = viewportProvider();
            suitMode = IsImageSmall(bitmap, viewport) ? 4 : 2;
        }

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

    private static bool IsImageSmall(Bitmap bitmap, BackgroundViewport viewport)
    {
        var width = viewport.Width > 0d
            ? viewport.Width
            : 850d;
        var height = viewport.Height > 0d
            ? viewport.Height
            : 500d;
        return bitmap.PixelSize.Width < width / 2d
            && bitmap.PixelSize.Height < height / 2d;
    }

    private void DisposeContent()
    {
        Brush = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }
}