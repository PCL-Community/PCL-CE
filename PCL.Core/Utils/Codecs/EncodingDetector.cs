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
    /// <returns>检测到的编码，有BOM的时候返回BOM对应的编码，没有BOM但传入的内容是合法的UTF-8的时候，返回UTF-8，其余情况返回GB18030</returns>
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
            return _DetectByBom(stream, originalPosition) ?? _DetectWithoutBOM(stream, originalPosition) ?? Encodings.GB18030;
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

        // 对样本进行分析
        if (sampleLength >= 3 && buffer is [0xef, 0xbb, 0xbf, ..])
            return Encoding.UTF8; // UTF-8

        if (sampleLength >= 2)
        {
            if (buffer is [0xfe, 0xff, ..])
                return Encoding.BigEndianUnicode; // UTF-16 BE
            if (buffer is [0xff, 0xfe, ..])
            {
                if (sampleLength >= 4 && buffer is [_, _, 0x00, 0x00])
                    return Encoding.UTF32; // UTF-32 LE
                return Encoding.Unicode;   // UTF-16 LE
            }
        }

        if (sampleLength >= 4)
        {
            if (buffer is [0x00, 0x00, 0xfe, 0xff])
                return Encoding.GetEncoding("utf-32BE"); // UTF-32 BE
            if (buffer is [0xff, 0xfe, 0x00, 0x00])
                return Encoding.UTF32; // UTF-32 LE
        }

        return null;
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
    /// 验证流内容是否为合法 UTF-8（严格解码，遇到非法字节则判定不是UTF-8）
    /// </summary>
    private static bool _IsValidUtf8(Stream stream, long originalPosition)
    {
        const int sampleSize = 1024;
        var buffer = new byte[sampleSize];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(sampleSize)];
        var decoder = new UTF8Encoding(false, true).GetDecoder();
        stream.Position = originalPosition;

        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                decoder.GetChars(buffer, 0, read, chars, 0, false);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}