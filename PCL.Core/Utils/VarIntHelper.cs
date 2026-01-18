using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Utils;

public static class VarIntHelper
{
    /// <summary>
    /// 将无符号长整数编码为VarInt字节序列
    /// </summary>
    /// <param name="value">要编码的64位无符号整数</param>
    /// <returns>VarInt字节数组</returns>
    public static byte[] Encode(ulong value)
    {
        using var stream = new MemoryStream();
        do
        {
            var temp = (byte)(value & 0x7F); // 取低7位
            value >>= 7;                      // 右移7位
            if (value != 0)                   // 如果还有后续数据
                temp |= 0x80;                 // 设置最高位为1
            stream.WriteByte(temp);
        } while (value != 0);
        
        return stream.ToArray();
    }

    /// <summary>
    /// 将无符号整数编码为VarInt字节序列
    /// </summary>
    /// <param name="value">要编码的32位无符号整数</param>
    /// <returns>VarInt字节数组</returns>
    public static byte[] Encode(uint value) => Encode((ulong)value);

    /// <summary>
    /// 从字节数组中解码无符号长整数
    /// </summary>
    /// <param name="bytes">包含VarInt编码的字节数组</param>
    /// <param name="readLength">读取的字节长度</param>
    /// <returns>解码后的64位无符号整数</returns>
    /// <exception cref="ArgumentNullException">输入字节数组为空</exception>
    /// <exception cref="FormatException">VarInt格式无效或超过最大长度</exception>
    public static ulong Decode(byte[] bytes, out int readLength)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        ulong result = 0;
        var shift = 0;
        var bytesRead = 0;
        const int maxBytes = 10; // ulong最大需要10字节
        
        foreach (var b in bytes)
        {
            if (bytesRead >= maxBytes)
                throw new FormatException("VarInt exceeds maximum length");
            
            // 取低7位并移位合并
            result |= (ulong)(b & 0x7F) << shift;
            bytesRead++;
            
            // 检查是否结束
            if ((b & 0x80) == 0)
            {
                readLength = bytesRead;
                return result;
            }

            shift += 7;
        }

        throw new FormatException("Incomplete VarInt encoding");
    }

    /// <summary>
    /// 从字节数组中解码无符号整数
    /// </summary>
    /// <param name="bytes">包含VarInt编码的字节数组</param>
    /// <param name="readLength">读取的字节长度</param>
    /// <returns>解码后的32位无符号整数</returns>
    public static uint DecodeUInt(byte[] bytes, out int readLength)
    {
        var result = Decode(bytes, out readLength);
        if (result > uint.MaxValue)
            throw new OverflowException("Decoded value exceeds UInt32 range");
        return (uint)result;
    }

    /// <summary>
    /// 从流中读取并解码无符号长整数，并将流前进所读取的字节数
    /// </summary>
    /// <param name="stream">输入流</param>
    /// <param name="cancellationToken">要监视取消请求的标记</param>
    /// <returns>解码后的64位无符号整数</returns>
    /// <exception cref="EndOfStreamException">流提前结束</exception>
    /// <exception cref="FormatException">VarInt格式无效</exception>
    public static async Task<ulong> ReadFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ulong result = 0;
        var shift = 0;
        var bytesRead = 0;
        const int maxBytes = 10;
        var buffer = new byte[1];
        while (true)
        {
            var readLength = await stream.ReadAsync(buffer, 0, 1, cancellationToken);
            if (readLength == 0)
                throw new EndOfStreamException();

            var b = buffer[0];
            bytesRead++;

            if (bytesRead > maxBytes)
                throw new FormatException("VarInt exceeds maximum length");

            result |= (ulong)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                return result;

            shift += 7;
        }
    }

    /// <summary>
    /// 从流中读取并解码无符号整数，并将流前进所读取的字节数
    /// </summary>
    /// <param name="stream">输入流</param>
    /// <param name="cancellationToken"></param>
    /// <returns>解码后的32位无符号整数</returns>
    public static async Task<uint> ReadUIntFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var result = await ReadFromStreamAsync(stream, cancellationToken);
        if (result > uint.MaxValue)
            throw new OverflowException("Decoded value exceeds UInt32 range");
        return (uint)result;
    }
    
    public static long DecodeZigZag(ulong value) => (long)(value >> 1) ^ -(long)(value & 1);

    public static ulong EncodeZigZag(ulong value) => ((value << 1) ^ (value >> 63));
}