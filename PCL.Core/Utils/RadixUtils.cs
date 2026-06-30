using System;
using System.Numerics;
using System.Text;

namespace PCL.Core.Utils;

/// <summary>
///     提供 2~65 进制文本转换能力。
/// </summary>
public static class RadixUtils
{
    private const string DefaultDigits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz/+=";

    /// <summary>
    ///     将文本从一个进制转换到另一个进制。默认字符集兼容 PCL 历史 2~65 进制定义。
    /// </summary>
    public static string Convert(
        string? input,
        int sourceRadix,
        int targetRadix,
        string? digits = null)
    {
        digits ??= DefaultDigits;
        _ValidateRadix(sourceRadix, digits, nameof(sourceRadix));
        _ValidateRadix(targetRadix, digits, nameof(targetRadix));

        if (string.IsNullOrEmpty(input)) return "0";

        input = input.Trim();
        if (input.Length == 0) return "0";

        var negative = input[0] == '-';
        if (negative) input = input[1..];
        if (input.Length == 0) return "0";

        var value = BigInteger.Zero;
        foreach (var ch in input)
        {
            var digit = digits.IndexOf(ch, StringComparison.Ordinal);
            if (digit < 0 || digit >= sourceRadix)
                throw new ArgumentOutOfRangeException(nameof(input), $"字符 '{ch}' 不在 {sourceRadix} 进制范围内。");
            value = value * sourceRadix + digit;
        }

        if (value.IsZero) return "0";

        var builder = new StringBuilder();
        while (value > BigInteger.Zero)
        {
            value = BigInteger.DivRem(value, targetRadix, out var remainder);
            builder.Insert(0, digits[(int)remainder]);
        }

        return negative ? "-" + builder : builder.ToString();
    }

    private static void _ValidateRadix(int radix, string digits, string paramName)
    {
        if (radix < 2 || radix > digits.Length)
            throw new ArgumentOutOfRangeException(paramName, radix, $"进制必须位于 2 到 {digits.Length} 之间。");
    }
}