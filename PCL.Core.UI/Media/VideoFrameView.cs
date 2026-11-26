using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PCL.Core.Media;

namespace PCL.Core.UI.Media;

/// <summary>按配置的适配模式绘制视频原始帧的控件。</summary>
public sealed class VideoFrameView : Control
{
    private const int MaxTileColumns = 64;
    private const int MaxTileRows = 64;

    private readonly WriteableBitmap _bitmap;
    private readonly int _height;
    private readonly int _rowBytes;
    private readonly int _width;
    private bool _disposed;
    private bool _hasFrame;
    private VideoSuitMode _suitMode;

    public VideoFrameView(int width, int height, VideoSuitMode suitMode)
    {
        _width = width;
        _height = height;
        _rowBytes = width * 4;
        _suitMode = suitMode;
        _bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96d, 96d),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        ClipToBounds = true;
        IsHitTestVisible = false;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.LowQuality);
    }

    /// <summary>当前适配模式。</summary>
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

    /// <summary>将一帧 BGRA 数据写入位图并请求重绘。</summary>
    public void Present(byte[] frame)
    {
        if (_disposed || frame.Length < _rowBytes * _height) return;

        using var buffer = _bitmap.Lock();
        if (buffer.RowBytes == _rowBytes)
            Marshal.Copy(frame, 0, buffer.Address, _rowBytes * _height);
        else
            for (var y = 0; y < _height; y++)
                Marshal.Copy(
                    frame,
                    y * _rowBytes,
                    IntPtr.Add(buffer.Address, y * buffer.RowBytes),
                    _rowBytes);

        _hasFrame = true;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_disposed || !_hasFrame) return;

        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0d || bounds.Height <= 0d) return;

        switch (_suitMode)
        {
            case VideoSuitMode.Smart when _IsSmall(bounds):
                _DrawTiled(context, bounds);
                break;

            case VideoSuitMode.Center:
                _DrawCentered(context, bounds);
                break;

            case VideoSuitMode.Stretch:
                _DrawFrame(context, bounds);
                break;

            case VideoSuitMode.Tile:
                _DrawTiled(context, bounds);
                break;

            case VideoSuitMode.TopLeft:
                _DrawFrame(context, new Rect(0d, 0d, _width, _height));
                break;

            case VideoSuitMode.TopRight:
                _DrawFrame(context, new Rect(bounds.Width - _width, 0d, _width, _height));
                break;

            case VideoSuitMode.BottomLeft:
                _DrawFrame(context, new Rect(0d, bounds.Height - _height, _width, _height));
                break;

            case VideoSuitMode.BottomRight:
                _DrawFrame(
                    context,
                    new Rect(
                        bounds.Width - _width,
                        bounds.Height - _height,
                        _width,
                        _height));
                break;

            case VideoSuitMode.Fit:
            default:
                _DrawCover(context, bounds);
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _bitmap.Dispose();
    }

    private bool _IsSmall(Rect bounds)
    {
        return _width < bounds.Width / 2d && _height < bounds.Height / 2d;
    }

    private void _DrawCover(DrawingContext context, Rect bounds)
    {
        var scale = Math.Max(bounds.Width / _width, bounds.Height / _height);
        if (scale <= 0d) return;

        var width = _width * scale;
        var height = _height * scale;
        _DrawFrame(
            context,
            new Rect(
                (bounds.Width - width) / 2d,
                (bounds.Height - height) / 2d,
                width,
                height));
    }

    private void _DrawCentered(DrawingContext context, Rect bounds)
    {
        _DrawFrame(
            context,
            new Rect(
                (bounds.Width - _width) / 2d,
                (bounds.Height - _height) / 2d,
                _width,
                _height));
    }

    private void _DrawTiled(DrawingContext context, Rect bounds)
    {
        var columns = Math.Min(MaxTileColumns, (int)Math.Ceiling(bounds.Width / _width));
        var rows = Math.Min(MaxTileRows, (int)Math.Ceiling(bounds.Height / _height));
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
            _DrawFrame(
                context,
                new Rect(
                    column * (double)_width,
                    row * (double)_height,
                    _width,
                    _height));
    }

    private void _DrawFrame(DrawingContext context, Rect destination)
    {
        context.DrawImage(
            _bitmap,
            new Rect(0d, 0d, _width, _height),
            destination);
    }
}