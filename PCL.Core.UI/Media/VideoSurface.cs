using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.Media;

namespace PCL.Core.UI.Media;

/// <summary>视频帧显示区域使用的绘制目标。</summary>
public readonly record struct VideoDrawOperation(Rect Source, Rect Destination);

/// <summary>视频背景帧显示的公共基类，负责适配模式和目标矩形计算。</summary>
public abstract class VideoSurface : Control, IDisposable
{
    private const int MaxTileColumns = 64;
    private const int MaxTileRows = 64;

    private double _blurRadius;
    private bool _rendererUnavailable;
    private VideoSuitMode _suitMode;

    protected VideoSurface(int width, int height, VideoSuitMode suitMode)
    {
        FrameWidth = Math.Max(1, width);
        FrameHeight = Math.Max(1, height);
        _suitMode = suitMode;
        ClipToBounds = true;
        IsHitTestVisible = false;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.LowQuality);
    }

    public int FrameWidth { get; }

    public int FrameHeight { get; }

    public abstract FfmpegPixelFormat PixelFormat { get; }

    public virtual bool SupportsShaderBlur => false;

    public VideoSuitMode SuitMode
    {
        get => _suitMode;
        set
        {
            if (_suitMode == value) return;
            _suitMode = value;
            InvalidateVisual();
        }
    }

    public double BlurRadius
    {
        get => _blurRadius;
        set
        {
            if (Math.Abs(_blurRadius - value) < 0.01d) return;
            _blurRadius = Math.Max(0d, value);
            InvalidateVisual();
        }
    }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>当前渲染后端无法绘制 YUV 帧时触发。</summary>
    public event Action? RendererUnavailable;

    public abstract void Present(byte[] frame);

    protected abstract void DisposeCore();

    protected IReadOnlyList<VideoDrawOperation> GetDrawOperations(Rect bounds)
    {
        return bounds.Width <= 0d || bounds.Height <= 0d
            ? []
            : SuitMode switch
            {
                VideoSuitMode.Smart when IsFrameSmall(bounds)
                    => GetTileOperations(bounds),
                VideoSuitMode.Center
                    => [GetCenteredOperation(bounds)],
                VideoSuitMode.Stretch
                    => [new VideoDrawOperation(GetSourceRect(), bounds)],
                VideoSuitMode.Tile
                    => GetTileOperations(bounds),
                VideoSuitMode.TopLeft
                    =>
                    [
                        new VideoDrawOperation(
                            GetSourceRect(),
                            new Rect(0d, 0d, FrameWidth, FrameHeight))
                    ],
                VideoSuitMode.TopRight
                    =>
                    [
                        new VideoDrawOperation(
                            GetSourceRect(),
                            new Rect(bounds.Width - FrameWidth, 0d, FrameWidth, FrameHeight))
                    ],
                VideoSuitMode.BottomLeft
                    =>
                    [
                        new VideoDrawOperation(
                            GetSourceRect(),
                            new Rect(0d, bounds.Height - FrameHeight, FrameWidth, FrameHeight))
                    ],
                VideoSuitMode.BottomRight
                    =>
                    [
                        new VideoDrawOperation(
                            GetSourceRect(),
                            new Rect(
                                bounds.Width - FrameWidth,
                                bounds.Height - FrameHeight,
                                FrameWidth,
                                FrameHeight))
                    ],
                _ => [GetCoverOperation(bounds)]
            };
    }

    protected bool IsFrameSmall(Rect bounds)
    {
        return FrameWidth < bounds.Width / 2d
            && FrameHeight < bounds.Height / 2d;
    }

    protected void NotifyRendererUnavailable()
    {
        if (_rendererUnavailable) return;
        _rendererUnavailable = true;
        Dispatcher.UIThread.Post(() => RendererUnavailable?.Invoke());
    }

    private VideoDrawOperation GetSourceOperation(Rect destination)
    {
        return new VideoDrawOperation(GetSourceRect(), destination);
    }

    private Rect GetSourceRect()
    {
        return new Rect(0d, 0d, FrameWidth, FrameHeight);
    }

    private VideoDrawOperation GetCoverOperation(Rect bounds)
    {
        var scale = Math.Max(
            bounds.Width / FrameWidth,
            bounds.Height / FrameHeight);
        var width = FrameWidth * scale;
        var height = FrameHeight * scale;
        return GetSourceOperation(
            new Rect(
                (bounds.Width - width) / 2d,
                (bounds.Height - height) / 2d,
                width,
                height));
    }

    private VideoDrawOperation GetCenteredOperation(Rect bounds)
    {
        return GetSourceOperation(
            new Rect(
                (bounds.Width - FrameWidth) / 2d,
                (bounds.Height - FrameHeight) / 2d,
                FrameWidth,
                FrameHeight));
    }

    public IReadOnlyList<VideoDrawOperation> GetTileOperations(Rect bounds)
    {
        var columns = Math.Min(
            MaxTileColumns,
            Math.Max(1, (int)Math.Ceiling(bounds.Width / FrameWidth)));
        var rows = Math.Min(
            MaxTileRows,
            Math.Max(1, (int)Math.Ceiling(bounds.Height / FrameHeight)));
        var operations = new List<VideoDrawOperation>(columns * rows);
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
            operations.Add(
                GetSourceOperation(
                    new Rect(
                        column * (double)FrameWidth,
                        row * (double)FrameHeight,
                        FrameWidth,
                        FrameHeight)));

        return operations;
    }
}