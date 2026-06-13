// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Text;

namespace PCL.Core.Utils.Codecs;

public static class EncodingDetector
{
    private const int MaxSampleLength = 64 * 1024;
    private static readonly UTF8Encoding _StrictUtf8 = new(false, true);
    private static readonly Encoding _StrictGb2312;

    static EncodingDetector()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _StrictGb2312 = Encoding.GetEncoding(
            936,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }

    /// <summary>
    /// 检测流中的文本编码方式。检测完成后恢复原始流位置。
    /// </summary>
    public static Encoding DetectEncoding(Stream stream, bool readFromBegin = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("流必须支持读操作", nameof(stream));
        if (!stream.CanSeek)
            throw new ArgumentException("流必须支持 Seek 操作", nameof(stream));

        var originalPosition = stream.Position;
        if (readFromBegin)
            stream.Position = 0;

        var readableLength = Math.Min(
            MaxSampleLength,
            Math.Max(0, stream.Length - stream.Position));
        var rented = ArrayPool<byte>.Shared.Rent(Math.Max(1, checked((int)readableLength)));
        try
        {
            var read = stream.ReadAtLeast(
                rented.AsSpan(0, (int)readableLength),
                (int)readableLength,
                throwOnEndOfStream: false);
            return DetectEncoding(rented.AsSpan(0, read));
        }
        finally
        {
            stream.Position = originalPosition;
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public static Encoding DetectEncoding(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return DetectEncoding(bytes.AsSpan());
    }

    public static Encoding DetectEncoding(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8;
        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            return new UTF32Encoding(bigEndian: true, byteOrderMark: true);
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
            return Encoding.UTF32;
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode;

        if (_CanDecode(_StrictUtf8, bytes))
            return Encoding.UTF8;
        if (_CanDecode(_StrictGb2312, bytes))
            return Encoding.GetEncoding(936);
        return Encoding.Default;
    }

    private static bool _CanDecode(Encoding encoding, ReadOnlySpan<byte> bytes)
    {
        try
        {
            _ = encoding.GetCharCount(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
