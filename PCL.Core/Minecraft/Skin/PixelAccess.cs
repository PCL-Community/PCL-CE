using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace PCL.Core.Minecraft.Skin;

/// <summary>
/// 基于 <see cref="Bitmap.LockBits"/> 的像素级读写辅助，统一以 32 位 ARGB 格式访问像素。
/// </summary>
internal static class PixelAccess
{
    /// <summary>
    /// 读取指定像素的 ARGB 值。单次调用会对整张位图加锁/解锁，适合少量采样。
    /// </summary>
    /// <param name="bitmap">目标位图。</param>
    /// <param name="x">像素 X 坐标。</param>
    /// <param name="y">像素 Y 坐标。</param>
    /// <returns>像素的 ARGB 值（alpha 在高位）。</returns>
    public static int GetPixel(Bitmap bitmap, int x, int y)
    {
        using var accessor = Lock(bitmap, ImageLockMode.ReadOnly);
        return accessor.GetPixel(x, y);
    }

    /// <summary>
    /// 写入指定像素的 ARGB 值。单次调用会对整张位图加锁/解锁，适合少量写入。
    /// </summary>
    /// <param name="bitmap">目标位图。</param>
    /// <param name="x">像素 X 坐标。</param>
    /// <param name="y">像素 Y 坐标。</param>
    /// <param name="argb">要写入的 ARGB 值（alpha 在高位）。</param>
    public static void SetPixel(Bitmap bitmap, int x, int y, int argb)
    {
        using var accessor = Lock(bitmap, ImageLockMode.ReadWrite);
        accessor.SetPixel(x, y, argb);
    }

    /// <summary>
    /// 锁定位图以获得批量访问句柄。使用完毕后应释放句柄以解锁位图。
    /// </summary>
    /// <param name="bitmap">目标位图。</param>
    /// <param name="mode">锁定模式。</param>
    /// <returns>位图访问句柄。</returns>
    public static PixelAccessor Lock(Bitmap bitmap, ImageLockMode mode)
    {
        return new PixelAccessor(bitmap, mode);
    }

    /// <summary>
    /// 将源区域像素拷贝到目标区域，可选水平翻转。
    /// </summary>
    /// <param name="source">源访问句柄。</param>
    /// <param name="dest">目标访问句柄。</param>
    /// <param name="srcX">源区域左上角 X。</param>
    /// <param name="srcY">源区域左上角 Y。</param>
    /// <param name="width">区域宽度。</param>
    /// <param name="height">区域高度。</param>
    /// <param name="dstX">目标区域左上角 X。</param>
    /// <param name="dstY">目标区域左上角 Y。</param>
    /// <param name="flipHorizontal">是否水平翻转。</param>
    public static void CopyRegion(PixelAccessor source, PixelAccessor dest,
        int srcX, int srcY, int width, int height, int dstX, int dstY, bool flipHorizontal)
    {
        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var sourceX = flipHorizontal ? srcX + (width - 1 - col) : srcX + col;
                dest.SetPixel(dstX + col, dstY + row, source.GetPixel(sourceX, srcY + row));
            }
        }
    }
}

/// <summary>
/// 位图锁定句柄：构造时锁定位图，释放时解锁。以 32 位 ARGB 访问像素。
/// </summary>
internal sealed unsafe class PixelAccessor : IDisposable
{
    private readonly Bitmap _bitmap;
    private readonly BitmapData _data;
    private readonly byte* _base;
    private readonly int _stride;

    /// <summary>
    /// 位图宽度。
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// 位图高度。
    /// </summary>
    public int Height { get; }

    internal PixelAccessor(Bitmap bitmap, ImageLockMode mode)
    {
        _bitmap = bitmap;
        Width = bitmap.Width;
        Height = bitmap.Height;
        _data = bitmap.LockBits(new Rectangle(0, 0, Width, Height), mode, PixelFormat.Format32bppArgb);
        if (_data.Stride < 0)
        {
            // 自底向上的存储：指针定位到第一行，行步进取绝对值
            _base = (byte*)_data.Scan0 + _data.Stride * (Height - 1);
            _stride = -_data.Stride;
        }
        else
        {
            _base = (byte*)_data.Scan0;
            _stride = _data.Stride;
        }
    }

    /// <summary>
    /// 读取指定像素的 ARGB 值。
    /// </summary>
    /// <param name="x">像素 X 坐标。</param>
    /// <param name="y">像素 Y 坐标。</param>
    /// <returns>像素的 ARGB 值（alpha 在高位）。</returns>
    public int GetPixel(int x, int y)
    {
        var p = _base + y * _stride + x * 4;
        return (p[3] << 24) | (p[2] << 16) | (p[1] << 8) | p[0];
    }

    /// <summary>
    /// 写入指定像素的 ARGB 值。
    /// </summary>
    /// <param name="x">像素 X 坐标。</param>
    /// <param name="y">像素 Y 坐标。</param>
    /// <param name="argb">要写入的 ARGB 值（alpha 在高位）。</param>
    public void SetPixel(int x, int y, int argb)
    {
        var p = _base + y * _stride + x * 4;
        p[0] = (byte)argb;
        p[1] = (byte)(argb >> 8);
        p[2] = (byte)(argb >> 16);
        p[3] = (byte)(argb >> 24);
    }

    public void Dispose()
    {
        _bitmap.UnlockBits(_data);
    }
}
