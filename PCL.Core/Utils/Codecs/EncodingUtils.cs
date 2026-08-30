namespace PCL.Core.Utils.Codecs;

using System;
using System.Text;

public static class EncodingUtils
{
    public static bool IsDefaultEncodingUtf8() => Encoding.Default.CodePage == 65001;

    public static bool IsDefaultEncodingGbk() => Encoding.Default.CodePage == 936;

    /// <summary>
    /// 解码字节切片为字符串，自动检测 BOM（UTF-8、UTF-16 LE/BE、UTF-32 LE/BE）或回退到 GB18030。
    /// </summary>
    /// <param name="span">要解码的字节切片。</param>
    /// <returns>解码后的字符串，失败时返回空字符串。</returns>
    public static string DecodeBytes(ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty)
            return string.Empty;

        // 1. 优先检查并跳过 BOM
        switch (span)
        {
            case [0x00, 0x00, 0xFE, 0xFF, ..]:
                return Encoding.GetEncoding("utf-32BE").GetString(span[4..]);

            case [0xFF, 0xFE, 0x00, 0x00, ..]:
                return Encoding.UTF32.GetString(span[4..]);

            case [0xEF, 0xBB, 0xBF, ..]:
                return Encoding.UTF8.GetString(span[3..]);

            case [0xFE, 0xFF, ..]:
                return Encoding.BigEndianUnicode.GetString(span[2..]);

            case [0xFF, 0xFE, ..]:
                return Encoding.Unicode.GetString(span[2..]);
        }

        // 2. 无 BOM：尝试对完整数据使用严格 UTF-8 解码
        try
        {
            var utf8Strict = Encoding.GetEncoding(
                Encoding.UTF8.CodePage,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);

            return utf8Strict.GetString(span);
        }
        catch (DecoderFallbackException)
        {
            // 3. 包含非法 UTF-8 字节（如 GB18030 汉字），回退到 GB18030
            return Encodings.GB18030.GetString(span);
        }
    }

    /// <summary>
    /// 解码字节数组为字符串，自动检测 BOM（UTF-8、UTF-16 LE/BE、UTF-32 LE/BE）或回退到 GB18030。
    /// </summary>
    /// <param name="bytes">要解码的字节数组。</param>
    /// <returns>解码后的字符串，失败时返回空字符串。</returns>
    public static string DecodeBytes(byte[] bytes)
    {
        return DecodeBytes(bytes.AsSpan());
    }
}