using System.Collections;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Utils;
using PCL.Core.Utils.Hash;

namespace PCL;

/// <summary>
///     PCL2 文本、兼容解析与展示格式化工具。用于替代调用点中的 ModBase 文本相关 API。
/// </summary>
public static class LauncherText
{
    public static char LeftQuote { get; } = Convert.ToChar(8220);
    public static char RightQuote { get; } = Convert.ToChar(8221);

    public static string GetStringFromEnum(Enum enumData)
    {
        return Enum.GetName(enumData.GetType(), enumData) ?? enumData.ToString();
    }

    public static string GetReadableFileSize(long fileSize)
    {
        return ByteStream.GetReadableLength(fileSize, provider: Lang.Culture);
    }

    public static double Val(object? value)
    {
        try
        {
            return value is "&" ? 0d : Conversion.Val(value);
        }
        catch
        {
            return 0d;
        }
    }

    public static string StrFill(string str, string code, byte length)
    {
        return TextUtils.LeftPadOrTrim(str, code, length);
    }

    public static object StrTrim(string str, bool removeQuote = true)
    {
        return TextUtils.TrimDisplayName(str, removeQuote);
    }

    public static ulong GetHash(string str)
    {
        var hash = 5381UL;
        for (var i = 0; i < str.Length; i++)
            hash = (hash << 5) ^ hash ^ str[i];
        return hash ^ 0xA98F501BC684032FUL;
    }

    public static string GetStringMD5(string str)
    {
        return BinaryEncoding.ToHexLower(MD5Provider.Instance.ComputeHash(str).AsSpan());
    }

    public static string EscapeXml(string value)
    {
        return TextUtils.EscapeXml(value);
    }

    public static string EscapeLikePattern(string value)
    {
        return TextUtils.EscapeLikePattern(value);
    }

    public static string RegexReplaceEach(string str, string regex, MatchEvaluator evaluator,
        RegexOptions options = RegexOptions.None)
    {
        return Regex.Replace(str, regex, evaluator, options);
    }

    public static List<string> RegexSearch(string str, string regex, RegexOptions options = RegexOptions.None)
    {
        return Regex.Matches(str, regex, options).Select(match => match.Value).ToList();
    }

    public static List<string> RegexSearch(string str, Regex regex)
    {
        return regex.Matches(str).Select(match => match.Value).ToList();
    }

    public static List<T> GetFullList<T>(IList data)
    {
        return CollectionUtils.FlattenMixedList<T>(data);
    }
}