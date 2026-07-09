using System;
using System.Globalization;
using System.IO;

namespace PCL.Core.IO;

public class ByteStream(Stream stream)
{
    private static readonly string[] _Units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB", "ZiB", "YiB"];
    public long Length => stream.Length;

    public string GetReadableLength()
    {
        return GetReadableLength(Length);
    }

    /// <summary>
    ///     格式化大小
    /// </summary>
    /// <param name="length">字节</param>
    /// <param name="startUnit">开始单位</param>
    /// <param name="provider">格式化区域提供程序，默认使用 <see cref="CultureInfo.InvariantCulture" /></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string GetReadableLength(long length, int startUnit = 0, IFormatProvider? provider = null)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startUnit, _Units.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(startUnit, 0);

        var isNegative = length < 0;
        decimal absBytes = isNegative ? -length : length;

        if (absBytes == 0)
            return "0 B";

        var unitIndex = startUnit;
        var value = absBytes;

        while (value >= 1024 && unitIndex < _Units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        if (unitIndex >= _Units.Length)
            throw new ArgumentOutOfRangeException(nameof(length),
                "Value too large for predefined units.");

        var sign = isNegative ? "-" : "";
        return $"{sign}{value.ToString("0.##", provider ?? CultureInfo.InvariantCulture)} {_Units[unitIndex]}";
    }
}