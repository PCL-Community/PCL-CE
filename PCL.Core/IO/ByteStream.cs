using System;
using System.IO;

namespace PCL.Core.IO;

public class ByteStream(Stream stream)
{
    public long Length => stream.Length;

    public string GetReadableLength() => GetReadableLength(this.Length);
    
    private static readonly string[] _Units = ["B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];
    /// <summary>
    /// 格式化大小
    /// </summary>
    /// <param name="length">字节</param>
    /// <param name="startUnit">开始单位</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string GetReadableLength(long length, int startUnit = 0)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startUnit, _Units.Length, nameof(startUnit));
        ArgumentOutOfRangeException.ThrowIfLessThan(startUnit, 0, nameof(startUnit));

        bool isNegative = length < 0;
        decimal absBytes = isNegative ? -length : length;

        if (absBytes == 0)
            return "0 B";

        int unitIndex = startUnit;
        decimal value = absBytes;

        while (value >= 1024 && unitIndex < _Units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        if (unitIndex >= _Units.Length)
            throw new ArgumentOutOfRangeException(nameof(length),
                "Value too large for predefined units.");

        string sign = isNegative ? "-" : "";
        return $"{sign}{value:0.##} {_Units[unitIndex]}";
    }
}