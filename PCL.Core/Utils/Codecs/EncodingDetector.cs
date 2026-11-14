using System;
using System.IO;
using System.Text;

namespace PCL.Core.Utils.Codecs;

public static class EncodingDetector
{
    /// <summary>
    /// 检测流中的文本编码方式（支持 Seek 的流）
    /// </summary>
    /// <param name="stream">输入流，必须支持 Seek</param>
    /// <param name="readFromBegin">是否将流重置到起始点</param>
    /// <returns>检测到的编码，未识别时返回 UTF-8 或系统默认</returns>
    public static Encoding DetectEncoding(Stream stream, bool readFromBegin = false)
    {
        if (!stream.CanRead)
            throw new ArgumentException("流必须支持读操作");
        if (!stream.CanSeek)
            throw new ArgumentException("流必须支持 Seek 操作");

        var originalPosition = stream.Position;
        if (readFromBegin) stream.Seek(0, SeekOrigin.Begin);

        try
        {
            return _DetectByBom(stream, originalPosition) ?? _DetectWithoutBOM(stream, originalPosition) ?? Encoding.Default;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    public static Encoding DetectEncoding(byte[] bytes)
    {
        return DetectEncoding(new MemoryStream(bytes), true);
    }

    /// <summary>
    /// 根据 BOM 判断编码
    /// </summary>
    private static Encoding? _DetectByBom(Stream stream, long originalPosition)
    {
        stream.Position = originalPosition;
        // 获取最长样本长度
        var readableLength = stream.Length - stream.Position;
        var sampleLength = Math.Min(readableLength, 4);
        var buffer = new byte[sampleLength];
        var actualRead = stream.Read(buffer, 0, buffer.Length);
        if (actualRead != sampleLength) throw new Exception("无法获取样本长度");

        return sampleLength switch
        {
            >= 3 when buffer is [0xef, 0xbb, 0xbf, ..] => Encoding.UTF8, // UTF-8

            >= 2 when buffer is [0xfe, 0xff, ..] => Encoding.BigEndianUnicode, // UTF-16 BE

            >= 2 when buffer is [0xff, 0xfe, ..] => sampleLength >= 4
                && buffer is [_, _, 0x00, 0x00]
                    ? Encoding.UTF32 // UTF-32 LE
                    : Encoding.Unicode, // UTF-16 LE

            _ => sampleLength < 4
                ? null
                : buffer switch
                {
                    [0x00, 0x00, 0xfe, 0xff] => Encoding.GetEncoding("utf-32BE"),
                    [0xff, 0xfe, 0x00, 0x00] => Encoding.UTF32,
                    _ => null
                }
        };
    }

    /// <summary>
    /// BOM 不存在时的备用检测策略
    /// </summary>
    private static Encoding? _DetectWithoutBOM(Stream stream, long originalPosition)
    {
        // 尝试验证是否为有效 UTF-8
        return _IsValidUtf8(stream, originalPosition) ? Encoding.UTF8 : null;
    }

    /// <summary>
    /// 验证流内容是否为合法 UTF-8（通过 round-trip 验证）
    /// </summary>
    private static bool _IsValidUtf8(Stream stream, long originalPosition)
    {
        const int sampleSize = 1024;
        var buffer = new byte[sampleSize];
        stream.Position = originalPosition;

        try
        {
            var read = stream.Read(buffer, 0, sampleSize);
            if (read == 0) return true;
            var sample = buffer.AsSpan(0, read);
            var decoded = Encoding.UTF8.GetString(sample);
            var roundTrip = Encoding.UTF8.GetBytes(decoded);
            return roundTrip.AsSpan().SequenceEqual(sample);
        }
        catch
        {
            return false;
        }
    }
}