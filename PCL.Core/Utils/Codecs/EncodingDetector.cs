using System;
using System.IO;
using System.Text;

namespace PCL.Core.Utils.Codecs;

public static class EncodingDetector
{
    static EncodingDetector()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    ///     检测流中的文本编码方式（支持 Seek 的流）
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
        var startPosition = readFromBegin
            ? 0
            : originalPosition;

        try
        {
            return _DetectByBom(stream, startPosition)
                   ?? _DetectWithoutBOM(stream, startPosition)
                   ?? Encoding.Default;
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
    ///     根据 BOM 判断编码
    /// </summary>
    private static Encoding? _DetectByBom(Stream stream, long startPosition)
    {
        stream.Position = startPosition;

        var readableLength = stream.Length - stream.Position;
        var sampleLength = (int)Math.Min(readableLength, 4);

        if (sampleLength <= 0)
            return null;

        var buffer = new byte[sampleLength];
        var actualRead = stream.Read(buffer, 0, buffer.Length);

        if (actualRead != sampleLength)
            throw new Exception("无法获取样本长度");

        ReadOnlySpan<byte> span = buffer.AsSpan(0, actualRead);

        return span switch
        {
            [0x00, 0x00, 0xFE, 0xFF, ..] => Encoding.GetEncoding("utf-32BE"), // UTF-32 BE
            [0xFF, 0xFE, 0x00, 0x00, ..] => Encoding.UTF32, // UTF-32 LE
            [0xEF, 0xBB, 0xBF, ..] => Encoding.UTF8, // UTF-8
            [0xFE, 0xFF, ..] => Encoding.BigEndianUnicode, // UTF-16 BE
            [0xFF, 0xFE, ..] => Encoding.Unicode, // UTF-16 LE
            _ => null
        };
    }

    /// <summary>
    ///     BOM 不存在时的备用检测策略
    /// </summary>
    private static Encoding? _DetectWithoutBOM(Stream stream, long startPosition)
    {
        // 尝试验证是否为有效 UTF-8
        if (_IsValidEncoding(stream, startPosition, Encoding.UTF8))
            return Encoding.UTF8;

        // 尝试验证是否为有效 GB18030 / GBK / GB2312
        try
        {
            var gb = Encodings.GB18030;
            if (_IsValidEncoding(stream, startPosition, gb))
                return gb;
        }
        catch
        {
            // 忽略编码不可用
        }

        return null;
    }

    /// <summary>
    ///     验证流内容在指定编码下是否合法
    /// </summary>
    private static bool _IsValidEncoding(
        Stream stream,
        long startPosition,
        Encoding encoding)
    {
        const int sampleSize = 1024;

        stream.Position = startPosition;

        var readableLength = (int)Math.Min(stream.Length - startPosition, sampleSize);
        if (readableLength <= 0)
            return true;

        var buffer = new byte[readableLength];
        var actualRead = stream.Read(buffer, 0, readableLength);

        if (actualRead <= 0)
            return true;

        try
        {
            var strictEncoding = Encoding.GetEncoding(
                encoding.CodePage,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);

            var validLength = actualRead;

            if (actualRead == sampleSize &&
                encoding.CodePage == Encoding.UTF8.CodePage)
            {
                // 如果样本末尾截断了 UTF-8 多字节字符，则排除未完成的字节序列。
                var i = actualRead - 1;

                while (i >= 0 &&
                       i >= actualRead - 4 &&
                       (buffer[i] & 0xC0) == 0x80)
                    i--;

                if (i >= 0 &&
                    i >= actualRead - 4 &&
                    (buffer[i] & 0x80) != 0)
                {
                    var expectedBytes = buffer[i] switch
                    {
                        var b when (b & 0xE0) == 0xC0 => 2,
                        var b when (b & 0xF0) == 0xE0 => 3,
                        var b when (b & 0xF8) == 0xF0 => 4,
                        _ => 1
                    };

                    if (actualRead - i < expectedBytes)
                        validLength = i;
                }
            }

            strictEncoding.GetCharCount(buffer, 0, validLength);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}