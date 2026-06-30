using System;
using System.Text;

namespace PCL.Core.Utils;

/// <summary>
///     通用文本处理工具。
/// </summary>
public static class TextUtils
{
    /// <summary>
    ///     将首字符转为大写，其余字符转为小写。空文本保持原样。
    /// </summary>
    public static string CapitalizeInvariant(string? word)
    {
        if (string.IsNullOrEmpty(word)) return string.Empty;
        return word[..1].ToUpperInvariant() + word[1..].ToLowerInvariant();
    }

    /// <summary>
    ///     将文本统一到指定长度：过长时截取左侧指定长度，过短时在左侧填充指定字符。
    /// </summary>
    public static string LeftPadOrTrim(string value, string padding, int length)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (value.Length > length) return value[..length];
        if (value.Length == length) return value;

        var padChar = string.IsNullOrEmpty(padding) ? ' ' : padding[0];
        return value.PadLeft(length, padChar);
    }

    /// <summary>
    ///     移除展示名称首尾常见标点，并可移除括号或冒号后的补充说明。
    /// </summary>
    public static string TrimDisplayName(string value, bool removeQuote = true)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (removeQuote)
        {
            value = _CutBefore(value, "（");
            value = _CutBefore(value, "：");
            value = _CutBefore(value, "(");
            value = _CutBefore(value, ":");
        }

        return value.Trim('.', '。', '！', ' ', '!', '?', '？', '\r', '\n');
    }

    /// <summary>
    ///     对 XML 特殊字符进行转义。
    /// </summary>
    public static string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("'", "&apos;")
            .Replace("\"", "&quot;")
            .Replace("\r\n", "&#xa;");
    }

    /// <summary>
    ///     为 Access/VB Like 风格通配字符添加方括号转义。
    /// </summary>
    public static string EscapeLikePattern(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var builder = new StringBuilder(input.Length);
        foreach (var c in input)
            if (c is '[' or ']' or '*' or '?' or '#')
                builder.Append('[').Append(c).Append(']');
            else
                builder.Append(c);

        return builder.ToString();
    }

    private static string _CutBefore(string value, string marker)
    {
        var index = value.IndexOf(marker, StringComparison.Ordinal);
        return index >= 0 ? value[..index] : value;
    }
}