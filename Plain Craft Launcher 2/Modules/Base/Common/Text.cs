using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Utils;
using PCL.Core.Utils.Hash;

namespace PCL;

public static partial class Common
{
    /// <summary>
    ///     返回一个枚举对应的字符串。
    /// </summary>
    /// <param name="enumData">一个已经实例化的枚举类型。</param>
    public static string? GetStringFromEnum(Enum enumData)
    {
        return Enum.GetName(enumData.GetType(), enumData);
    }

    /// <summary>
    ///     将文件大小转化为适合的文本形式，如“1.28 M”。
    /// </summary>
    /// <param name="fileSize">以字节为单位的大小表示。</param>
    public static string GetString(long fileSize)
    {
        return ByteStream.GetReadableLength(fileSize, provider: Lang.Culture);
    }

    /// <summary>
    ///     获取 JSON 对象。
    /// </summary>
    public static JsonNode GetJson(string data)
    {
        try
        {
            return JsonCompat.ParseNode(data);
        }
        catch (Exception ex)
        {
            var dataText = data ?? "";
            var length = dataText.Length;
            throw new Exception(
                $"格式化 JSON 失败：{(length > 2000
                    ? $"{dataText[..500]}...(全长 {length} 个字符)...{dataText[(length - 500)..]}"
                    : dataText)}",
                ex);
        }
    }

    /// <summary>
    ///     将第一个字符转换为大写，其余字符转换为小写。
    /// </summary>
    public static string Capitalize(this string word)
    {
        return string.IsNullOrEmpty(word)
            ? word
            : word[..1].ToUpperInvariant() + word[1..].ToLowerInvariant();
    }

    /// <summary>
    ///     将字符串统一至某个长度，过短则以 Code 将其右侧填充，过长则截取靠左的指定长度。
    /// </summary>
    public static string StrFill(string str, string code, byte length)
    {
        return str.Length > length
            ? str[..length]
            : str.PadRight(length, code[0])[str.Length..] + str;
    }

    /// <summary>
    ///     移除字符串首尾的标点符号、回车，以及括号中、冒号后的补充说明内容。
    /// </summary>
    public static object StrTrim(string str, bool removeQuote = true)
    {
        if (removeQuote)
            str = str.Split("（")[0].Split("：")[0].Split("(")[0].Split(":")[0];
        return str.Trim('.', '。', '！', ' ', '!', '?', '？', '\r', '\n');
    }

    /// <summary>
    ///     连接字符串。
    /// </summary>
    public static string Join(this IEnumerable list, string split)
    {
        var builder = new StringBuilder();
        var isFirst = true;
        foreach (var element in list)
        {
            if (isFirst)
                isFirst = false;
            else
                builder.Append(split);
            if (element is not null)
                builder.Append(element);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     获取字符串哈希值。
    /// </summary>
    public static ulong GetHash(string str)
    {
        var getHashRet = 5381UL;
        for (int i = 0, loopTo = str.Length - 1; i <= loopTo; i++)
            getHashRet = (getHashRet << 5) ^ getHashRet ^ str[i];
        return getHashRet ^ 0xA98F501BC684032FUL;
    }

    /// <summary>
    ///     获取字符串 MD5。
    /// </summary>
    public static string GetStringMd5(string str)
    {
        return (string)GetHexString(MD5Provider.Instance.ComputeHash(str));
    }

    extension(string input)
    {
        /// <summary>
        ///     检查字符串中的字符是否均为 ASCII 字符。
        /// </summary>
        public bool IsAscii()
        {
            return input.All(c => c < 128);
        }

        /// <summary>
        ///     获取在子字符串第一次出现之前的部分，例如对 2024/11/08 拆切 / 会得到 2024。
        ///     如果未找到子字符串则不裁切。
        /// </summary>
        public string BeforeFirst(string text, bool ignoreCase = false)
        {
            var pos = string.IsNullOrEmpty(text)
                ? -1
                : input.IndexOfF(text, ignoreCase);
            return pos >= 0
                ? input[..pos]
                : input;
        }

        /// <summary>
        ///     获取在子字符串最后一次出现之前的部分，例如对 2024/11/08 拆切 / 会得到 2024/11。
        ///     如果未找到子字符串则不裁切。
        /// </summary>
        public string BeforeLast(string text, bool ignoreCase = false)
        {
            var pos = string.IsNullOrEmpty(text)
                ? -1
                : input.LastIndexOfF(text, ignoreCase);
            return pos >= 0
                ? input[..pos]
                : input;
        }

        /// <summary>
        ///     获取在子字符串第一次出现之后的部分，例如对 2024/11/08 拆切 / 会得到 11/08。
        ///     如果未找到子字符串则不裁切。
        /// </summary>
        public string AfterFirst(string text, bool ignoreCase = false)
        {
            var pos = string.IsNullOrEmpty(text)
                ? -1
                : input.IndexOfF(text, ignoreCase);
            return pos >= 0
                ? input[(pos + text.Length)..]
                : input;
        }

        /// <summary>
        ///     获取在子字符串最后一次出现之后的部分，例如对 2024/11/08 拆切 / 会得到 08。
        ///     如果未找到子字符串则不裁切。
        /// </summary>
        public string AfterLast(string text, bool ignoreCase = false)
        {
            var pos = string.IsNullOrEmpty(text)
                ? -1
                : input.LastIndexOfF(text, ignoreCase);
            return pos >= 0
                ? input[(pos + text.Length)..]
                : input;
        }

        /// <summary>
        ///     获取处于两个子字符串之间的部分，裁切尽可能多的内容。
        ///     等效于 AfterLast 后接 BeforeFirst。
        ///     如果未找到子字符串则不裁切。
        /// </summary>
        public string Between(string after, string before, bool ignoreCase = false)
        {
            var startPos = string.IsNullOrEmpty(after)
                ? -1
                : input.LastIndexOfF(after, ignoreCase);

            if (startPos >= 0)
                startPos += after.Length;
            else
                startPos = 0;

            var endPos = string.IsNullOrEmpty(before)
                ? -1
                : input.IndexOfF(before, startPos, ignoreCase);

            if (endPos >= 0)
                return input.Substring(startPos, endPos - startPos);
            if (startPos > 0)
                return input[startPos..];

            return input;
        }

        /// <summary>
        ///     高速的 StartsWith。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool StartsWithF(string prefix, bool ignoreCase = false)
        {
            return input.StartsWith(
                prefix,
                ignoreCase
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }

        /// <summary>
        ///     高速的 EndsWith。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EndsWithF(string suffix, bool ignoreCase = false)
        {
            return input.EndsWith(
                suffix,
                ignoreCase
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }

        /// <summary>
        ///     支持可变大小写判断的 Contains。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsF(string subStr, bool ignoreCase = false)
        {
            return input.Contains(
                subStr,
                ignoreCase
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }

        /// <summary>
        ///     高速的 IndexOf。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOfF(string subStr, bool ignoreCase = false)
        {
            return input.IndexOf(
                subStr,
                ignoreCase
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }

        /// <summary>
        ///     高速的 IndexOf。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOfF(string subStr, int startIndex, bool ignoreCase = false)
        {
            return input.IndexOf(
                subStr,
                startIndex,
                ignoreCase
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }

        /// <summary>
        ///     高速的 LastIndexOf。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int LastIndexOfF(string subStr, bool ignoreCase = false)
        {
            return input.LastIndexOf(
                subStr,
                ignoreCase
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     不会报错的 Val。
    ///     如果输入有误，返回 0。
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

    /// <summary>
    ///     为字符串进行 XML 转义。
    /// </summary>
    public static string EscapeXml(string str)
    {
        if (str.StartsWithF("{"))
            str = "{}" + str;
        return str
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("'", "&apos;")
            .Replace("\"", "&quot;")
            .Replace("\r\n", "&#xa;");
    }

    /// <summary>
    ///     为字符串进行 Like 关键字转义。
    /// </summary>
    public static string EscapeLikePattern(string input)
    {
        var sb = new StringBuilder();
        foreach (var c in input)
            switch (c)
            {
                case '[' or ']' or '*' or '?' or '#':
                {
                    sb.Append('[').Append(c).Append(']');
                    break;
                }
                default:
                {
                    sb.Append(c);
                    break;
                }
            }

        return sb.ToString();
    }

    /// <param name="str">要搜索的字符串</param>
    extension(string str)
    {
        /// <summary>
        ///     搜索字符串中的所有正则匹配项。
        /// </summary>
        public List<string> RegexSearch(string regex, RegexOptions options = RegexOptions.None)
        {
            List<string> regexSearchRet;
            try
            {
                regexSearchRet = [];
                var regexSearchRes = new Regex(regex, options).Matches(str);
                if (regexSearchRes is null)
                    return regexSearchRet;
                foreach (Match item in regexSearchRes)
                    regexSearchRet.Add(item.Value);
            }
            catch (Exception ex)
            {
                Log(ex, "正则匹配全部项出错");
                return [];
            }

            return regexSearchRet;
        }

        /// <summary>
        ///     搜索字符串中的所有正则匹配项。
        /// </summary>
        /// <param name="regex">正则表达式对象</param>
        /// <returns>所有匹配项的列表</returns>
        public List<string> RegexSearch(Regex regex)
        {
            try
            {
                var result = new List<string>();
                foreach (Match item in regex.Matches(str)) result.Add(item.Value);
                return result;
            }
            catch (Exception ex)
            {
                Log(ex, "正则匹配全部项出错");
                return [];
            }
        }

        /// <summary>
        ///     获取字符串中的第一个正则匹配项，若无匹配则返回 Nothing。
        /// </summary>
        public string? RegexSeek(string regex, RegexOptions options = RegexOptions.None)
        {
            try
            {
                var result = Regex.Match(str, regex, options).Value;
                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch (Exception ex)
            {
                Log(ex, "正则匹配第一项出错");
                return null;
            }
        }

        /// <summary>
        ///     获取字符串中的第一个正则匹配项，若无匹配则返回 Nothing。
        /// </summary>
        public string? RegexSeek(Regex regex, RegexOptions options = RegexOptions.None)
        {
            try
            {
                var result = regex.Match(str, (int)options).Value;
                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch (Exception ex)
            {
                Log(ex, "正则匹配第一项出错");
                return null;
            }
        }

        /// <summary>
        ///     检查字符串是否匹配某正则模式。
        /// </summary>
        public bool RegexCheck(string regex, RegexOptions options = RegexOptions.None)
        {
            try
            {
                return Regex.IsMatch(str, regex, options);
            }
            catch (Exception ex)
            {
                Log(ex, "正则检查出错");
                return false;
            }
        }

        /// <summary>
        ///     进行正则替换，会抛出错误。
        /// </summary>
        public string RegexReplace(
            string searchRegex,
            string replaceTo,
            RegexOptions options = RegexOptions.None)
        {
            return Regex.Replace(str, searchRegex, replaceTo, options);
        }

        /// <summary>
        ///     对每个正则匹配分别进行替换，会抛出错误。
        /// </summary>
        public string RegexReplaceEach(
            string searchRegex,
            MatchEvaluator replaceTo,
            RegexOptions options = RegexOptions.None)
        {
            return Regex.Replace(str, searchRegex, replaceTo, options);
        }
    }
}
