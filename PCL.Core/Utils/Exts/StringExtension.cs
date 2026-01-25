using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PCL.Core.Utils.Exts;

public static class StringExtension
{
    public static object? Convert(string? value, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        if (targetType == typeof(string)) return value;

        if (value is null)
        {
            if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null) return null;
            return Activator.CreateInstance(targetType);
        }

        var converter = TypeDescriptor.GetConverter(targetType);

        if (converter.CanConvertFrom(typeof(string)))
        {
            var c = converter.ConvertFromInvariantString(value);
            return c;
        }

        if (typeof(IConvertible).IsAssignableFrom(targetType))
        {
            // ReSharper disable once RedundantSuppressNullableWarningExpression
            var changed = System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture)!;
            return changed;
        }

        if (targetType.IsEnum) return Enum.Parse(targetType, value, ignoreCase: true);

        var parse = targetType.GetMethod("Parse", 
            BindingFlags.Public | BindingFlags.Static,
            binder: null, types: [typeof(string)], modifiers: null);
        if (parse is not null) return parse.Invoke(null, [value]);

        throw new NotSupportedException($"无法将字符串转换为类型 {targetType.FullName}");
    }

    public static T? Convert<T>(this string? value)
    {
        var obj = Convert(value, typeof(T));
        if (obj is null) return default;
        return (T)obj;
    }

    public static string? ConvertToString(object? obj)
    {
        if (obj == null) return null;
        if (obj is string s) return s;

        var converter = TypeDescriptor.GetConverter(obj.GetType());
        if (converter.CanConvertTo(typeof(string)))
        {
            object? o = converter.ConvertToInvariantString(obj);
            return o as string;
        }

        if (obj is IFormattable fmt) return fmt.ToString(null, CultureInfo.InvariantCulture);

        return obj.ToString();
    }

    public static string? ConvertToString<T>(this T? value) => ConvertToString((object?)value);

    private static readonly char[] _B36Map = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

    extension(string input)
    {
        public string FromB10ToB36()
        {
            var n = BigInteger.Parse(input);
            var s = new List<char>();
            while (n > 0)
            {
                var i = (n % 36).ToByteArray()[0];
                s.Add(_B36Map[i]);
                n /= 36;
            }
            s.Reverse();
            return string.Join("", s);
        }

        public string FromB36ToB10()
        {
            var ns = input.Select(c => (c is >= '0' and <= '9') ? c - '0' : c - 'A' + 10).ToArray();
            var nb = ns.Aggregate(new BigInteger(0), (n, i) => n * 36 + i);
            return nb.ToString();
        }
    }

    private static readonly char[] _B32Map = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ".ToCharArray();

    extension(string input)
    {
        /// <summary>
        /// 将 Base10 文本重新编码为 Base32 文本。
        /// </summary>
        public string FromB10ToB32()
        {
            var n = BigInteger.Parse(input);
            var s = new List<char>();
            while (n > 0)
            {
                var i = (n % 32).ToByteArray()[0];
                s.Add(_B32Map[i]);
                n /= 32;
            }
            s.Reverse();
            return string.Join("", s);
        }

        /// <summary>
        /// 将 Base32 文本重新编码为 Base10 文本。
        /// </summary>
        public string FromB32ToB10()
        {
            var ns = input.Select(Parse).ToArray();
            var nb = ns.Aggregate(new BigInteger(0), (n, i) => n * 32 + i);
            return nb.ToString();

            int Parse(char c) => c switch
            {
                >= '2' and <= '9' => c - '2',
                >= 'A' and <= 'H' => c - 'A' + 8,
                >= 'J' and <= 'N' => c - 'J' + 16,
                >= 'P' and <= 'Z' => c - 'P' + 21,
                _ => throw new ArgumentOutOfRangeException(nameof(input), $"Character '{c}' out of Base32 range")
            };
        }
    }

    extension(string input)
    {
          
        public T ParseToEnum<T>() where T : struct, Enum
        {
            if (String.IsNullOrWhiteSpace(input))
            {
                return (T)(object)0;
            }
            else if (int.TryParse(input, out int numericValue))
            {
                return (T)(object)numericValue;
            }
            else
            {
                return Enum.Parse<T>(input, true);
            }
        }
    }

    extension([NotNullWhen(false)] string? value)
    {
        /// <summary>
        /// <see cref="string.IsNullOrEmpty"/> 的扩展方法。
        /// </summary>
        public bool IsNullOrEmpty() => string.IsNullOrEmpty(value);

        /// <summary>
        /// <see cref="string.IsNullOrWhiteSpace"/> 的扩展方法。
        /// </summary>
        public bool IsNullOrWhiteSpace() => string.IsNullOrWhiteSpace(value);
    }

    /// <param name="input">文本</param>
    extension(string? input)
    {
        /// <summary>
        /// 当文本为空时返回替代文本，否则返回原来的文本。
        /// </summary>
        /// <param name="replacement">替代文本</param>
        public string ReplaceNullOrEmpty(string? replacement = null)
            => string.IsNullOrEmpty(input) ? (replacement ?? string.Empty) : input;

        /// <summary>
        /// 替换指定文本中的所有换行符。
        /// </summary>
        /// <param name="replacement">用于替换的文本</param>
        /// <returns>替换后的文本</returns>
        public string ReplaceLineBreak(string replacement = " ")
            => input?.Replace(RegexPatterns.NewLine, replacement) ?? string.Empty;

        /// <summary>
        /// 替换指定文本中所有匹配正则表达式的部分。
        /// </summary>
        /// <param name="regex">正则表达式</param>
        /// <param name="replacement">用于替换的文本</param>
        /// <returns>替换后的文本</returns>
        [return: NotNullIfNotNull(nameof(input))]
        public string? Replace(Regex regex, string replacement)
            => input == null ? null : regex.Replace(input, replacement);

        /// <summary>
        /// 判断指定文本是否能成功匹配正则表达式。
        /// </summary>
        /// <param name="regex">正则表达式</param>
        /// <returns>若匹配成功则为 <c>true</c>，若文本为 <c>null</c> 或匹配不成功则为 <c>false</c></returns>
        public bool IsMatch(Regex regex)
            => input != null && regex.IsMatch(input);
    }

    extension(string str)
    {
        /// <summary>
        /// 查找并返回指定文本中所有与正则表达式匹配的部分。
        /// </summary>
        public List<string> RegexSearch(Regex regex)
        {
            var result = new List<string>();
            var regexSearchRes = regex.Matches(str);
            if (regexSearchRes.Count == 0) return result;
            result.AddRange(from Match item in regexSearchRes select item.Value);
            return result;
        }

        /// <summary>
        /// 判断指定文本是否在 ASCII 范围内。
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public bool IsASCII()
        {
            return str.All(c => c < 128);
        }

        public bool StartsWithF(string prefix, bool ignoreCase = false)
            => str.StartsWith(prefix, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        public bool EndsWithF(string suffix, bool ignoreCase = false)
            => str.EndsWith(suffix, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        public bool ContainsF(string subStr, bool ignoreCase = false)
            => str.Contains(subStr, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        public int IndexOfF(string subStr, bool ignoreCase = false)
            => str.IndexOf(subStr, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        public int IndexOfF(string subStr, int startIndex, bool ignoreCase = false)
            => str.IndexOf(subStr, startIndex, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        public int LastIndexOfF(string subStr, bool ignoreCase = false)
            => str.LastIndexOf(subStr, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        public int LastIndexOfF(string subStr, int startIndex, bool ignoreCase = false)
            => str.LastIndexOf(subStr, startIndex, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
