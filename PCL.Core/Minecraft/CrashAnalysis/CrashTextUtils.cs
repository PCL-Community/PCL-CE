using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     <p>崩溃分析专用的文本工具集合。</p>
///     <p>
///         这里集中处理换行归一化、head/tail 截断和安全的子串提取，避免规则或准备流程
///         再次出现旧版按字符拆分换行、O(n²) 去重等问题。
///     </p>
/// </summary>
public static class CrashTextUtils
{
    public static string NormalizeNewLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    public static IReadOnlyList<string> ReadLinesNormalized(string text)
    {
        return NormalizeNewLines(text).Split('\n');
    }

    public static string HeadTailDistinct(string text, int headLines, int tailLines)
    {
        return HeadTailDistinct(ReadLinesNormalized(text), headLines, tailLines);
    }

    public static string HeadTailDistinct(IReadOnlyList<string> rawLines, int headLines, int tailLines)
    {
        if (rawLines.Count == 0) return string.Empty;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = new List<string>(Math.Min(rawLines.Count, headLines + tailLines));

        if (rawLines.Count <= headLines + tailLines)
        {
            foreach (var line in rawLines) AddLine(line);
            return string.Join("\n", lines);
        }

        var viewedLines = -1;
        var realHeadLines = 0;
        for (var i = 0; i < rawLines.Count; i++)
        {
            viewedLines = i;
            var beforeCount = lines.Count;
            AddLine(rawLines[i]);
            if (lines.Count != beforeCount) realHeadLines++;
            if (realHeadLines >= headLines) break;
        }

        var realTailLines = 0;
        for (var i = rawLines.Count - 1; i > viewedLines; i--)
        {
            var beforeCount = lines.Count;
            AddLine(rawLines[i], true);
            if (lines.Count != beforeCount) realTailLines++;
            if (realTailLines >= tailLines) break;
        }

        return string.Join("\n", lines);

        void AddLine(string line, bool insertBeforeTail = false)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            if (!seen.Add(line)) return;

            if (insertBeforeTail)
                lines.Insert(Math.Min(lines.Count, headLines), line);
            else
                lines.Add(line);
        }
    }

    public static string? MatchFirst(string text, string pattern, RegexOptions options = RegexOptions.None)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var match = Regex.Match(text, pattern, options, TimeSpan.FromMilliseconds(500));
        return match.Success
            ? match.Value
            : null;
    }

    public static IReadOnlyList<string> MatchAll(string text, string pattern, RegexOptions options = RegexOptions.None)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var matches = Regex
            .Matches(text, pattern, options, TimeSpan.FromMilliseconds(500));
        return matches
            .Select(static match => match.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    public static string Between(string text, string start, string end)
    {
        var startIndex = text.IndexOf(start, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0) return string.Empty;

        startIndex += start.Length;
        var endIndex = text.IndexOf(end, startIndex, StringComparison.OrdinalIgnoreCase);
        return endIndex < 0
            ? text[startIndex..]
            : text[startIndex..endIndex];
    }

    public static string BeforeFirst(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? text
            : text[..index];
    }

    public static string AfterFirst(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? string.Empty
            : text[(index + marker.Length)..];
    }

    public static string AfterLast(string text, string marker)
    {
        var index = text.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? string.Empty
            : text[(index + marker.Length)..];
    }

    public static byte[] Utf8Bytes(string text)
    {
        return Encoding.UTF8.GetBytes(text);
    }
}