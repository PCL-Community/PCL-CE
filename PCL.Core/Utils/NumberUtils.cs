using System;
using System.Globalization;

namespace PCL.Core.Utils;

/// <summary>
///     通用数值处理工具。
/// </summary>
public static class NumberUtils
{
    /// <summary>
    ///     将浮点数舍入并限制到 <see cref="byte" /> 可表示的 0~255 范围。
    /// </summary>
    public static byte ClampToByte(double value)
    {
        if (double.IsNaN(value)) return 0;
        value = Math.Clamp(value, 0d, 255d);
        return (byte)Math.Round(value);
    }

    /// <summary>
    ///     返回数值符号：正数为 1，负数为 -1，0 或 NaN 为 0。
    /// </summary>
    public static int Sign(double value)
    {
        if (value is 0d or double.NaN) return 0;
        return value > 0d ? 1 : -1;
    }

    /// <summary>
    ///     在两个数值之间做线性插值。
    /// </summary>
    public static double Lerp(
        double start,
        double end,
        double progress,
        int digits = 6)
    {
        var value = start * (1d - progress) + end * progress;
        return digits >= 0 ? Math.Round(value, digits) : value;
    }

    /// <summary>
    ///     将对象转换为 double；解析失败时返回 0。
    ///     对字符串保留旧 VB Val 风格的“读取开头数值片段”语义，例如“123abc”解析为 123。
    /// </summary>
    public static double ParseDoubleOrZero(object? value)
    {
        switch (value)
        {
            case null:
                return 0d;
            case double doubleValue:
                return doubleValue;
            case float floatValue:
                return floatValue;
            case decimal decimalValue:
                return (double)decimalValue;
            case IConvertible convertible and not string:
                try
                {
                    return convertible.ToDouble(CultureInfo.InvariantCulture);
                }
                catch
                {
                    // 继续尝试字符串解析。
                }

                break;
        }

        return ParseLeadingDoubleOrZero(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    ///     从字符串开头读取一个使用英文句点作为小数点的浮点数字面量；无法读取时返回 0。
    ///     该方法用于替代历史 VB Val 调用点，不会要求整个字符串都必须是数字。
    /// </summary>
    public static double ParseLeadingDoubleOrZero(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0d;

        text = text.TrimStart();
        if (text.Length == 0 || text[0] == '&') return 0d;

        var index = 0;
        if (text[index] is '+' or '-') index++;

        var hasDigit = false;
        while (index < text.Length && char.IsDigit(text[index]))
        {
            hasDigit = true;
            index++;
        }

        if (index < text.Length && text[index] == '.')
        {
            index++;
            while (index < text.Length && char.IsDigit(text[index]))
            {
                hasDigit = true;
                index++;
            }
        }

        if (!hasDigit) return 0d;

        var exponentEnd = _TryReadExponent(text, index);
        if (exponentEnd > index) index = exponentEnd;

        return double.TryParse(
            text[..index],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : 0d;
    }

    private static int _TryReadExponent(string text, int index)
    {
        if (index >= text.Length || text[index] is not ('e' or 'E')) return index;

        var cursor = index + 1;
        if (cursor < text.Length && text[cursor] is '+' or '-') cursor++;

        var digitStart = cursor;
        while (cursor < text.Length && char.IsDigit(text[cursor])) cursor++;

        return cursor > digitStart ? cursor : index;
    }

    /// <summary>
    ///     将数值限制在指定闭区间。
    /// </summary>
    public static double Clamp(double value, double min, double max)
    {
        return min > max
            ? min
            : Math.Clamp(value, min, max);
    }
}