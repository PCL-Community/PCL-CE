using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PCL.Core.Media;

namespace PCL.Core.UI.Media;

/// <summary>按配置的适配模式绘制 BGRA 视频原始帧的控件。</summary>
public sealed class VideoFrameView : VideoSurface
{
    private readonly WriteableBitmap _bitmap;
    private readonly int _rowBytes;
    private bool _hasFrame;

    public VideoFrameView(int width, int height, VideoSuitMode suitMode)
        : base(width, height, suitMode)
    {
        _rowBytes = FrameWidth * 4;
        _bitmap = new WriteableBitmap(
            new PixelSize(FrameWidth, FrameHeight),
            new Vector(96d, 96d),
            Avalonia.Platform.PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
    }

    public override FfmpegPixelFormat PixelFormat => FfmpegPixelFormat.Bgra8888;

    /// <summary>将一帧 BGRA 数据写入位图并请求重绘。</summary>
    public override void Present(byte[] frame)
    {
        if (IsDisposed || frame.Length < _rowBytes * FrameHeight) return;

        using var buffer = _bitmap.Lock();
        if (buffer.RowBytes == _rowBytes)
            Marshal.Copy(frame, 0, buffer.Address, _rowBytes * FrameHeight);
        else
            for (var y = 0; y < FrameHeight; y++)
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
        if (IsDisposed || !_hasFrame) return;

        var bounds = new Rect(Bounds.Size);
        foreach (var operation in GetDrawOperations(bounds))
            context.DrawImage(
                _bitmap,
                operation.Source,
                operation.Destination);
    }

    protected override void DisposeCore()
    {
        _bitmap.Dispose();
    }
}