using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Hash;

namespace PCL;

public static partial class ModBase
{
    #region 文本

    public static char vbLq = Convert.ToChar(8220);
    public static char vbRq = Convert.ToChar(8221);

    /// <summary>
    ///     返回一个枚举对应的字符串。
    /// </summary>
    /// <param name="enumData">一个已经实例化的枚举类型。</param>
    public static string GetStringFromEnum(Enum enumData)
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
            throw new Exception("格式化 JSON 失败：" + (length > 2000
                ? string.Concat(dataText.AsSpan(0, 500), $"...(全长 {length} 个字符)...", dataText.AsSpan(length - 500))
                : dataText), ex);
        }
    }

    /// <summary>
    ///     将第一个字符转换为大写，其余字符转换为小写。
    /// </summary>
    public static string Capitalize(this string word)
    {
        return TextUtils.CapitalizeInvariant(word);
    }

    /// <summary>
    ///     将字符串统一至某个长度，过短则以 Code 将其右侧填充，过长则截取靠左的指定长度。
    /// </summary>
    public static string StrFill(string str, string code, byte length)
    {
        return TextUtils.LeftPadOrTrim(str, code, length);
    }

    /// <summary>
    ///     将一个小数显示为固定的小数点后位数形式，将向零取整。
    ///     如 12 保留 2 位则输出 12.00，而 95.678 保留 2 位则输出 95.67。
    /// </summary>
    public static string StrFillNum(double num, int length)
    {
        return Lang.Number(num, $"F{length}");
    }

    /// <summary>
    ///     移除字符串首尾的标点符号、回车，以及括号中、冒号后的补充说明内容。
    /// </summary>
    public static object StrTrim(string str, bool removeQuote = true)
    {
        return TextUtils.TrimDisplayName(str, removeQuote);
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
    ///     分割字符串。
    /// </summary>
    public static string[] Split(this string fullStr, string splitStr)
    {
        return splitStr.Length == 1
            ? fullStr.Split(splitStr[0])
            : fullStr.Split([splitStr], StringSplitOptions.None);
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
    public static string GetStringMD5(string str)
    {
        return (string)GetHexString(MD5Provider.Instance.ComputeHash(str));
    }

    /// <summary>
    ///     检查字符串中的字符是否均为 ASCII 字符。
    /// </summary>
    public static bool IsASCII(this string input)
    {
        return input.All(c => c < 128);
    }

    /// <summary>
    ///     获取在子字符串第一次出现之前的部分，例如对 2024/11/08 拆切 / 会得到 2024。
    ///     如果未找到子字符串则不裁切。
    /// </summary>
    public static string BeforeFirst(this string str, string text, bool ignoreCase = false)
    {
        return StringSliceExtensions.BeforeFirst(str, text, ignoreCase);
    }

    /// <summary>
    ///     获取在子字符串最后一次出现之前的部分，例如对 2024/11/08 拆切 / 会得到 2024/11。
    ///     如果未找到子字符串则不裁切。
    /// </summary>
    public static string BeforeLast(this string str, string text, bool ignoreCase = false)
    {
        return StringSliceExtensions.BeforeLast(str, text, ignoreCase);
    }

    /// <summary>
    ///     获取在子字符串第一次出现之后的部分，例如对 2024/11/08 拆切 / 会得到 11/08。
    ///     如果未找到子字符串则不裁切。
    /// </summary>
    public static string AfterFirst(this string str, string text, bool ignoreCase = false)
    {
        return StringSliceExtensions.AfterFirst(str, text, ignoreCase);
    }

    /// <summary>
    ///     获取在子字符串最后一次出现之后的部分，例如对 2024/11/08 拆切 / 会得到 08。
    ///     如果未找到子字符串则不裁切。
    /// </summary>
    public static string AfterLast(this string str, string text, bool ignoreCase = false)
    {
        return StringSliceExtensions.AfterLast(str, text, ignoreCase);
    }

    /// <summary>
    ///     获取处于两个子字符串之间的部分，裁切尽可能多的内容。
    ///     等效于 AfterLast 后接 BeforeFirst。
    ///     如果未找到子字符串则不裁切。
    /// </summary>
    public static string Between(this string str, string after, string before, bool ignoreCase = false)
    {
        return StringSliceExtensions.Between(str, after, before, ignoreCase);
    }

    /// <summary>
    ///     高速的 StartsWith。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool StartsWithF(this string str, string prefix, bool ignoreCase = false)
    {
        return str.StartsWith(prefix, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    ///     高速的 EndsWith。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EndsWithF(this string str, string suffix, bool ignoreCase = false)
    {
        return str.EndsWith(suffix, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    ///     支持可变大小写判断的 Contains。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsF(this string str, string subStr, bool ignoreCase = false)
    {
        return str.IndexOf(subStr, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    ///     高速的 IndexOf。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfF(this string str, string subStr, bool ignoreCase = false)
    {
        return str.IndexOf(subStr, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    ///     高速的 IndexOf。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfF(this string str, string subStr, int startIndex, bool ignoreCase = false)
    {
        return str.IndexOf(subStr, startIndex,
            ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    ///     高速的 LastIndexOf。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LastIndexOfF(this string str, string subStr, bool ignoreCase = false)
    {
        return str.LastIndexOf(subStr, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    ///     高速的 LastIndexOf。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LastIndexOfF(this string str, string subStr, int startIndex, bool ignoreCase = false)
    {
        return str.LastIndexOf(subStr, startIndex,
            ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
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

    // 转义
    /// <summary>
    ///     为字符串进行 XML 转义。
    /// </summary>
    public static string EscapeXml(string str)
    {
        if (str.StartsWithF("{"))
            str = "{}" + str; // #4187
        return TextUtils.EscapeXml(str);
    }

    /// <summary>
    ///     为字符串进行 Like 关键字转义。
    /// </summary>
    public static string EscapeLikePattern(string input)
    {
        return TextUtils.EscapeLikePattern(input);
    }

    // 正则
    /// <summary>
    ///     搜索字符串中的所有正则匹配项。
    /// </summary>
    public static List<string> RegexSearch(this string str, string regex, RegexOptions options = RegexOptions.None)
    {
        try
        {
            return RegexExtensions.RegexSearch(str, regex, options);
        }
        catch (Exception ex)
        {
            Log(ex, "正则匹配全部项出错");
            return [];
        }
    }

    /// <summary>
    ///     搜索字符串中的所有正则匹配项。
    /// </summary>
    /// <param name="str">要搜索的字符串</param>
    /// <param name="regex">正则表达式对象</param>
    /// <returns>所有匹配项的列表</returns>
    public static List<string> RegexSearch(this string str, Regex regex)
    {
        try
        {
            return regex.Matches(str).Select(item => item.Value).ToList();
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
    public static string RegexSeek(this string str, string regex, RegexOptions options = RegexOptions.None)
    {
        try
        {
            return RegexExtensions.RegexSeek(str, regex, options);
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
    public static string RegexSeek(this string str, Regex regex, RegexOptions options = RegexOptions.None)
    {
        try
        {
            return RegexExtensions.RegexSeek(str, regex);
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
    public static bool RegexCheck(this string str, string regex, RegexOptions options = RegexOptions.None)
    {
        try
        {
            return RegexExtensions.RegexCheck(str, regex, options);
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
    public static string RegexReplace(this string allContents, string searchRegex, string replaceTo,
        RegexOptions options = RegexOptions.None)
    {
        return RegexExtensions.RegexReplace(allContents, searchRegex, replaceTo, options);
    }

    /// <summary>
    ///     对每个正则匹配分别进行替换，会抛出错误。
    /// </summary>
    public static string RegexReplaceEach(this string allContents, string searchRegex, MatchEvaluator replaceTo,
        RegexOptions options = RegexOptions.None)
    {
        return RegexExtensions.RegexReplaceEach(allContents, searchRegex, replaceTo, options);
    }

    #endregion
}