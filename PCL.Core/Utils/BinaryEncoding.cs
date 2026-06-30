using System;

namespace PCL.Core.Utils;

/// <summary>
///     二进制数据文本编码工具。
/// </summary>
public static class BinaryEncoding
{
    public static string ToHexLower(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string ToHexLower(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return ToHexLower(bytes.AsSpan());
    }
}