namespace PCL.Core.Utils.Codecs;

using System;
using System.Text;

public static class EncodingUtils {
    public static bool IsDefaultEncodingUtf8() => Encoding.Default.CodePage == 65001;
    
    public static bool IsDefaultEncodingGbk() => Encoding.Default.CodePage == 936;

    /// <summary>
    /// 解码字节数组为字符串，自动检测 BOM（UTF-8、UTF-16 LE/BE、UTF-32 LE/BE）或回退到 GB18030。
    /// </summary>
    /// <param name="bytes">要解码的字节数组。</param>
    /// <returns>解码后的字符串，失败时返回空字符串。</returns>
    public static string DecodeBytes(byte[] bytes) {
        if (bytes.Length == 0) return "";

        // 使用 EncodingDetector 检测编码
        var encoding = EncodingDetector.DetectEncoding(bytes);

        // 如果检测到有效编码（非 Encoding.Default），直接解码
        if (!encoding.Equals(Encoding.Default)) {
            ReadOnlySpan<byte> span = bytes.AsSpan();
            if (encoding.Equals(Encoding.UTF8) && span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF) {
                return encoding.GetString(span[3..]); // 跳过 UTF-8 BOM
            }
            if (encoding.Equals(Encoding.BigEndianUnicode) && span.Length >= 2 && span[0] == 0xFE && span[1] == 0xFF) {
                return encoding.GetString(span[2..]); // 跳过 UTF-16 BE BOM
            }
            if (encoding.Equals(Encoding.Unicode) && span.Length >= 2 && span[0] == 0xFF && span[1] == 0xFE) {
                return encoding.GetString(span[2..]); // 跳过 UTF-16 LE BOM
            }
            if (encoding.Equals(Encoding.UTF32) && span.Length >= 4 && span[0] == 0xFF && span[1] == 0xFE && span[2] == 0x00 && span[3] == 0x00) {
                return encoding.GetString(span[4..]); // 跳过 UTF-32 LE BOM
            }
            if (encoding.CodePage == Encoding.GetEncoding("utf-32BE").CodePage && span.Length >= 4 && span[0] == 0x00 && span[1] == 0x00 && span[2] == 0xFE && span[3] == 0xFF) {
                return encoding.GetString(span[4..]); // 跳过 UTF-32 BE BOM
            }
            return encoding.GetString(span); // 无 BOM 或其他编码
        }

        // 无 BOM 或检测为 Encoding.Default，尝试 UTF-8
        try {
            var utf8Result = Encoding.UTF8.GetString(bytes);
            return utf8Result.Contains('\uFFFD') ? Encodings.GB18030.GetString(bytes) : utf8Result; // 无效 UTF-8，回退到 GB18030
        } catch (DecoderFallbackException) {
            return Encodings.GB18030.GetString(bytes); // UTF-8 解码失败，回退到 GB18030
        }
    }
}
