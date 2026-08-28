using System.Drawing;
using System.Drawing.Imaging;

namespace PCL.Core.Minecraft.Skin;

/// <summary>
/// 皮肤规范化器，对应 HMCL 的 NormalizedSkin。
/// 将合法尺寸的皮肤（64x64、128x128 等）归一化为等宽正方形，并把旧格式（宽高比 2:1）重排为 64x64 布局。
/// 位图生命周期由调用方管理，本类型不负责释放。
/// </summary>
public sealed class NormalizedSkin
{
    /// <summary>
    /// 原始纹理。
    /// </summary>
    public Bitmap Texture { get; }

    /// <summary>
    /// 规范化后的纹理（宽 = 原宽，高 = 原宽）。
    /// </summary>
    public Bitmap NormalizedTexture { get; }

    /// <summary>
    /// 缩放系数，等于宽 / 64。
    /// </summary>
    public int Scale { get; }

    /// <summary>
    /// 是否为旧格式（宽高比为 2:1，如 64x32）。
    /// </summary>
    public bool IsOldFormat { get; }

    /// <summary>
    /// 根据原始纹理构造规范化皮肤。
    /// </summary>
    /// <param name="texture">原始皮肤纹理。</param>
    /// <exception cref="InvalidSkinException">纹理宽度不是 64 的倍数，或宽高比既不是 1:1 也不是 2:1 时抛出。</exception>
    public NormalizedSkin(Bitmap texture)
    {
        var w = texture.Width;
        var h = texture.Height;
        if (w % 64 != 0)
            throw new InvalidSkinException($"Invalid size {w}x{h}");
        if (w == h)
            IsOldFormat = false;
        else if (w == h * 2)
            IsOldFormat = true;
        else
            throw new InvalidSkinException($"Invalid size {w}x{h}");

        Scale = w / 64;
        Texture = texture;
        NormalizedTexture = new Bitmap(w, w, PixelFormat.Format32bppArgb);

        using (var source = PixelAccess.Lock(texture, ImageLockMode.ReadOnly))
        using (var dest = PixelAccess.Lock(NormalizedTexture, ImageLockMode.ReadWrite))
        {
            // 先整体拷贝原图到 (0,0)
            PixelAccess.CopyRegion(source, dest, 0, 0, w, h, 0, 0, flipHorizontal: false);
            if (IsOldFormat)
                ConvertOldSkin(source, dest);
        }
    }

    /// <summary>
    /// 判断是否为纤细（Alex）模型，依据 HMCL 的特定区域透明/纯黑特征。
    /// </summary>
    /// <returns>纤细模型返回 <c>true</c>，经典模型返回 <c>false</c>。</returns>
    public bool IsSlim()
    {
        // 统一加锁一次，循环内批量采样，避免逐像素 LockBits/UnlockBits
        using var accessor = PixelAccess.Lock(NormalizedTexture, ImageLockMode.ReadOnly);
        return HasTransparency(accessor, 50, 16, 2, 4)
            || HasTransparency(accessor, 54, 20, 2, 12)
            || HasTransparency(accessor, 42, 48, 2, 4)
            || HasTransparency(accessor, 46, 52, 2, 12)
            || (IsAreaBlack(accessor, 50, 16, 2, 4)
                && IsAreaBlack(accessor, 54, 20, 2, 12)
                && IsAreaBlack(accessor, 42, 48, 2, 4)
                && IsAreaBlack(accessor, 46, 52, 2, 12));
    }

    private bool HasTransparency(PixelAccessor accessor, int x0, int y0, int width, int height)
    {
        var s = Scale;
        for (var y = y0 * s; y < (y0 + height) * s; y++)
        {
            for (var x = x0 * s; x < (x0 + width) * s; x++)
            {
                if (((accessor.GetPixel(x, y) >> 24) & 0xff) != 0xff)
                    return true;
            }
        }
        return false;
    }

    private bool IsAreaBlack(PixelAccessor accessor, int x0, int y0, int width, int height)
    {
        var s = Scale;
        for (var y = y0 * s; y < (y0 + height) * s; y++)
        {
            for (var x = x0 * s; x < (x0 + width) * s; x++)
            {
                if ((uint)accessor.GetPixel(x, y) != 0xff000000u)
                    return false;
            }
        }
        return true;
    }

    private void ConvertOldSkin(PixelAccessor source, PixelAccessor dest)
    {
        var s = Scale;
        // 腿：top/bottom 为 4x4，其余面为 4x12
        PixelAccess.CopyRegion(source, dest, 4 * s, 16 * s, 4 * s, 4 * s, 20 * s, 48 * s, flipHorizontal: true);   // top
        PixelAccess.CopyRegion(source, dest, 8 * s, 16 * s, 4 * s, 4 * s, 24 * s, 48 * s, flipHorizontal: true);   // bottom
        PixelAccess.CopyRegion(source, dest, 0 * s, 20 * s, 4 * s, 12 * s, 24 * s, 52 * s, flipHorizontal: true);  // outer
        PixelAccess.CopyRegion(source, dest, 4 * s, 20 * s, 4 * s, 12 * s, 20 * s, 52 * s, flipHorizontal: true);  // front
        PixelAccess.CopyRegion(source, dest, 8 * s, 20 * s, 4 * s, 12 * s, 16 * s, 52 * s, flipHorizontal: true);  // inner
        PixelAccess.CopyRegion(source, dest, 12 * s, 20 * s, 4 * s, 12 * s, 28 * s, 52 * s, flipHorizontal: true); // back
        // 臂：top/bottom 为 4x4，其余面为 4x12
        PixelAccess.CopyRegion(source, dest, 44 * s, 16 * s, 4 * s, 4 * s, 36 * s, 48 * s, flipHorizontal: true);  // top
        PixelAccess.CopyRegion(source, dest, 48 * s, 16 * s, 4 * s, 4 * s, 40 * s, 48 * s, flipHorizontal: true);  // bottom
        PixelAccess.CopyRegion(source, dest, 40 * s, 20 * s, 4 * s, 12 * s, 40 * s, 52 * s, flipHorizontal: true); // outer
        PixelAccess.CopyRegion(source, dest, 44 * s, 20 * s, 4 * s, 12 * s, 36 * s, 52 * s, flipHorizontal: true); // front
        PixelAccess.CopyRegion(source, dest, 48 * s, 20 * s, 4 * s, 12 * s, 32 * s, 52 * s, flipHorizontal: true); // inner
        PixelAccess.CopyRegion(source, dest, 52 * s, 20 * s, 4 * s, 12 * s, 44 * s, 52 * s, flipHorizontal: true); // back
    }
}
