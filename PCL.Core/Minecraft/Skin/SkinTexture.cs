using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;

namespace PCL.Core.Minecraft.Skin;

/// <summary>
/// 皮肤纹理，对应 HMCL auth/offline 模块的 Texture。
/// 纹理以内容哈希标识，进程内按哈希缓存，相同内容只保留一份位图。
/// </summary>
public sealed class SkinTexture
{
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, SkinTexture> Cache = new();

    /// <summary>
    /// 纹理内容的 SHA-256 十六进制哈希（小写）。
    /// </summary>
    public string Hash { get; }

    /// <summary>
    /// 纹理位图。
    /// </summary>
    public Bitmap Image { get; }

    private SkinTexture(string hash, Bitmap image)
    {
        Hash = hash;
        Image = image;
    }

    /// <summary>
    /// 加载纹理：计算哈希并按哈希缓存。若已存在相同哈希的纹理，返回缓存项并忽略传入的位图；
    /// 缓存未命中时直接持有传入的位图（不做拷贝）。
    /// </summary>
    /// <param name="image">纹理位图。</param>
    /// <returns>已缓存的纹理实例。</returns>
    public static SkinTexture Load(Bitmap image)
    {
        var hash = ComputeHash(image);
        lock (CacheLock)
        {
            if (Cache.TryGetValue(hash, out var existing))
                return existing;
            var created = new SkinTexture(hash, image);
            Cache[hash] = created;
            return created;
        }
    }

    /// <summary>
    /// 获取指定哈希的纹理，未命中时返回 <c>null</c>。
    /// </summary>
    /// <param name="hash">纹理哈希。</param>
    /// <returns>对应的纹理，未命中为 <c>null</c>。</returns>
    public static SkinTexture? Get(string hash)
    {
        lock (CacheLock)
        {
            return Cache.TryGetValue(hash, out var value) ? value : null;
        }
    }

    /// <summary>
    /// 计算纹理哈希，算法与 HMCL Texture.computeTextureHash 一致：
    /// SHA256（宽 4 字节大端 + 高 4 字节大端 + 每像素 ARGB 各 1 字节；alpha 为 0 时 RGB 清零）。
    /// </summary>
    /// <param name="image">纹理位图。</param>
    /// <returns>小写十六进制哈希字符串。</returns>
    public static string ComputeHash(Bitmap image)
    {
        using var sha256 = SHA256.Create();

        var header = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(header, image.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height);
        sha256.TransformBlock(header, 0, header.Length, null, 0);

        using var accessor = PixelAccess.Lock(image, ImageLockMode.ReadOnly);
        var buffer = new byte[4096];
        var bufferIndex = 0;
        for (var y = 0; y < accessor.Height; y++)
        {
            for (var x = 0; x < accessor.Width; x++)
            {
                var pixel = accessor.GetPixel(x, y);
                var alpha = (byte)(pixel >> 24);
                var red = (byte)(pixel >> 16);
                var green = (byte)(pixel >> 8);
                var blue = (byte)pixel;
                if (alpha == 0)
                {
                    red = 0;
                    green = 0;
                    blue = 0;
                }
                buffer[bufferIndex++] = alpha;
                buffer[bufferIndex++] = red;
                buffer[bufferIndex++] = green;
                buffer[bufferIndex++] = blue;
                if (bufferIndex == buffer.Length)
                {
                    sha256.TransformBlock(buffer, 0, buffer.Length, null, 0);
                    bufferIndex = 0;
                }
            }
        }
        if (bufferIndex > 0)
            sha256.TransformBlock(buffer, 0, bufferIndex, null, 0);

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
    }
}
