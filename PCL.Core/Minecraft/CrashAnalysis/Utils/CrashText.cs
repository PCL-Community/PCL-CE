namespace PCL.Core.Minecraft.CrashAnalysis;

public static class CrashText
{
    public static string NormalizeNewLines(string text)
    {
        return string.IsNullOrEmpty(text)
            ? string.Empty
            : text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    public static IReadOnlyList<string> ReadLines(string text)
    {
        return NormalizeNewLines(text).Split('\n');
    }

    public static string GetWindow(IReadOnlyList<string> lines, int index, int before, int after)
    {
        if (lines.Count == 0) return string.Empty;
        var start = Math.Max(0, index - before);
        var end = Math.Min(lines.Count - 1, index + after);
        return string.Join("\n", lines.Skip(start).Take(end - start + 1));
    }

    public static string HeadTail(IReadOnlyList<string> lines, int headLines, int tailLines)
    {
        if (lines.Count <= headLines + tailLines)
            return string.Join("\n", lines.Where(static line => !string.IsNullOrWhiteSpace(line)));

        var result = new List<string>(headLines + tailLines);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in lines.Take(headLines)) Add(line);
        foreach (var line in lines.Skip(Math.Max(0, lines.Count - tailLines))) Add(line);
        return string.Join("\n", result);

        void Add(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            if (seen.Add(line)) result.Add(line);
        }
    }

    public static string TrimPreview(string? value, int maxLines, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var lines = ReadLines(value)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Take(maxLines)
            .ToList();
        var result = string.Join("\n", lines);
        return result.Length <= maxChars ? result : result[..maxChars] + "\n...";
    }


    public static string NormalizeEvidence(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return string.Join(" ", ReadLines(value)
                .Select(static line => line.Trim())
                .Where(static line => !string.IsNullOrWhiteSpace(line))
                .Where(static line => !line.StartsWith("at ", StringComparison.OrdinalIgnoreCase)))
            .Trim()
            .ToLowerInvariant();
    }

    public static string SummarizeEvidence(string? value, int maxLength = 180)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var line = ReadLines(value)
            .Select(static item => item.Trim())
            .FirstOrDefault(static item => !string.IsNullOrWhiteSpace(item) &&
                                           !item.StartsWith("at ", StringComparison.OrdinalIgnoreCase));
        line ??= value.Trim();
        line = line.Replace("\t", " ", StringComparison.Ordinal);
        line = string.Join(" ", line.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= maxLength ? line : line[..maxLength] + "...";
    }

    public static string EscapeMarkdownInline(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        return value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Trim();
    }

    public static string EscapeMarkdownCell(string? value)
    {
        return EscapeMarkdownInline(value);
    }

    public static string EscapeMarkdownParagraph(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return NormalizeNewLines(value)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Trim();
    }
}