using Humanizer;
using Microsoft.VisualBasic;
using PCL.Core.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PCL.Core.Utils.Exts;

public static class StringConvertExtension
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
}

public static class StringExtension
{
    public static string? ConvertToString(object? obj)
    {
        if (obj is null) return null;
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

        public string FromB64ToB64UrlSafe() => input.Replace("+", "-").Replace("/", "_");
        public string FromB64UrlSafeToB64() => input.Replace("-", "+").Replace("_", "/");

        public byte[] FromB64ToBytes()
        {
            switch (input.Length % 4)
            {
                case 3:
                    input += "===";
                    break;
                case 2:
                    input += "==";
                    break;
                case 1:
                    input += "=";
                    break;
            }

            return Convert.FromBase64String(input);
        }

        public byte[] FromB64UrlSafeToBytes() => input.FromB64UrlSafeToB64().FromB64ToBytes();
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

    extension(string input)
    {

        /// <summary>
        /// 查找并返回指定文本中所有与正则表达式匹配的部分。
        /// </summary>
        /// <remarks>
        /// 并不推荐。如果可用，建议使用 <see cref="GeneratedRegexAttribute"/> 来生成静态正则表达式实例以获得更好的性能。
        /// </remarks>
        public List<string> RegexSearch(Regex regex)
        {
            var result = new List<string>();
            var regexSearchRes = regex.Matches(input);

            if (regexSearchRes.Count == 0) return result;

            result.AddRange(from Match item in regexSearchRes select item.Value);

            return result;
        }


        /// <summary>
        /// 获取字符串中的第一个正则匹配项，若无匹配则返回 Nothing。
        /// </summary>
        public string? RegexSeek(string regex, RegexOptions options = RegexOptions.None)
        {
            try
            {
                var result = Regex.Match(input, regex, options).Value;
                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "正则匹配第一项出错");
                return null;
            }
        }

        /// <summary>
        /// 获取字符串中的第一个正则匹配项，若无匹配则返回 Nothing。
        /// </summary>
        public string? RegexSeek(Regex regex, RegexOptions options = RegexOptions.None)
        {
            try
            {
                var result = regex.Match(input, (int)options).Value;
                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "正则匹配第一项出错");
                return null;
            }
        }

        /// <summary>
        /// 检查字符串是否匹配某正则模式。
        /// </summary>
        public bool RegexCheck(string regex, RegexOptions options = RegexOptions.None)
        {
            try
            {
                return Regex.IsMatch(input, regex, options);
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "正则检查出错");
                return false;
            }
        }

        /// <summary>
        /// 进行正则替换，会抛出错误。
        /// </summary>
        public string RegexReplace(string searchRegex,
            string replaceTo,
            RegexOptions options = RegexOptions.None) =>
            Regex.Replace(input, searchRegex, replaceTo, options);

        /// <summary>
        /// 对每个正则匹配分别进行替换，会抛出错误。
        /// </summary>
        public string RegexReplaceEach(string searchRegex,
            MatchEvaluator replaceTo,
            RegexOptions options = RegexOptions.None) =>
            Regex.Replace(input, searchRegex, replaceTo, options);
    }

    extension(string str)
    {

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



    extension(string str)
    {


        /// <summary>
        /// 获取在子字符串第一次出现之前的部分<br/>
        /// 如果未找到子字符串则不裁切。
        /// </summary>
        /// <example>
        /// 例如对 "2024/11/08" 拆切 '/' 会得到 "2024"
        /// </example>
        public string BeforeFirst(string text, bool ignoreCase = false)
        {
            var pos = string.IsNullOrEmpty(text) ? -1 : str.IndexOfF(text, ignoreCase);
            if (pos >= 0) return str[..pos];

            return str;
        }

        /// <summary>
        /// 获取在子字符串最后一次出现之前的部分<br/>
        /// 如果未找到子字符串则不裁切。
        /// </summary>
        /// <example>
        /// 例如对 "2024/11/08" 拆切 '/' 会得到 "2024/11"。
        /// </example>
        public string BeforeLast(string text, bool ignoreCase = false)
        {
            var pos = string.IsNullOrEmpty(text) ? -1 : str.LastIndexOfF(text, ignoreCase);
            if (pos >= 0) return str[..pos];

            return str;
        }

        /// <summary>
        /// 获取在子字符串第一次出现之后的部分<br/>
        /// 如果未找到子字符串则不裁切。
        /// </summary>
        /// <example>
        /// 例如对 "2024/11/08" 拆切 '/' 会得到 "11/08"。
        /// </example>
        public string AfterFirst(string text, bool ignoreCase = false)
        {
            var pos = string.IsNullOrEmpty(text) ? -1 : str.IndexOfF(text, ignoreCase);
            if (pos >= 0) return str[(pos + text.Length)..];

            return str;
        }

        /// <summary>
        /// 获取在子字符串最后一次出现之后的部分<br/>
        /// 如果未找到子字符串则不裁切。
        /// </summary>
        /// <example>
        /// 例如对 "2024/11/08" 拆切 '/' 会得到 "08"。
        /// </example>
        public string AfterLast(string text, bool ignoreCase = false)
        {
            var pos = string.IsNullOrEmpty(text) ? -1 : str.LastIndexOfF(text, ignoreCase);
            if (pos >= 0) return str[(pos + text.Length)..];

            return str;
        }


        /// <summary>
        /// 获取处于两个子字符串之间的部分，裁切尽可能多的内容。
        /// 等效于 <see cref="AfterLast"/> 后接 <seealso cref="BeforeFirst"/>。
        /// 如果未找到子字符串则不裁切。
        /// </summary>
        public string Between(string after, string before, bool ignoreCase = false)
        {
            var startPos = string.IsNullOrEmpty(after) ? -1 : str.LastIndexOfF(after, ignoreCase);
            if (startPos >= 0)
            {
                startPos += after.Length;
            }
            else
            {
                startPos = 0;
            }

            var endPos = string.IsNullOrEmpty(before) ? -1 : str.IndexOfF(before, startPos, ignoreCase);
            if (endPos >= 0)
            {
                return str.Substring(startPos, endPos - startPos);
            }

            if (startPos > 0)
            {
                return str[startPos..];
            }

            return str;
        }
    }

    /// <summary>
    /// Humanize the string by capitalizing the first letter and lowercasing the rest.
    /// </summary>
    extension(string input)
    {

        /// <summary>
        /// 将第一个字符转换为大写，其余字符转换为小写。
        /// </summary>
        public string Capitalize()
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return input.Humanize(LetterCasing.Sentence);
        }

        /// <summary>
        /// 将字符串统一至某个长度，过短则以 Code 将其右侧填充，过长则截取靠左的指定长度。
        /// </summary>
        public string Truncate(string code, byte length) => input.Truncate(length, code);
    }



    /// <summary>
    /// 不会报错的 Val。
    /// 如果输入有误，返回 0。
    /// </summary>
    public static double Val(object str)
    {
        try
        {
            return str is "&" ? 0d : Conversion.Val(str);
        }
        catch
        {
            return 0d;
        }
    }

}
