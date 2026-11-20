using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PCL.Media;

/// <summary>以覆盖方式绘制视频原始帧的控件。</summary>
public sealed class VideoFrameView : Control
{
    private readonly WriteableBitmap _bitmap;
    private readonly int _height;
    private readonly int _rowBytes;
    private readonly int _width;
    private bool _disposed;
    private bool _hasFrame;

    public VideoFrameView(int width, int height)
    {
        _width = width;
        _height = height;
        _rowBytes = width * 4;
        _bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96d, 96d),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        ClipToBounds = true;
        IsHitTestVisible = false;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.LowQuality);
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

        var scale = Math.Max(bounds.Width / _width, bounds.Height / _height);
        if (scale <= 0d) return;

        var width = _width * scale;
        var height = _height * scale;
        context.DrawImage(
            _bitmap,
            new Rect(0d, 0d, _width, _height),
            new Rect(
                (bounds.Width - width) / 2d,
                (bounds.Height - height) / 2d,
                width,
                height));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _bitmap.Dispose();
    }
}