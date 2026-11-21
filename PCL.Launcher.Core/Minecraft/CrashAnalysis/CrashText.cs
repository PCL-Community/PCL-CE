namespace PCL;

internal static class CrashText
{
    public static string BeforeFirst(
        string text,
        string marker,
        bool ignoreCase = false)
    {
        var pos = string.IsNullOrEmpty(marker)
            ? -1
            : text.IndexOf(marker, _Comparison(ignoreCase));
        return pos >= 0
            ? text[..pos]
            : text;
    }

    public static string AfterFirst(
        string text,
        string marker,
        bool ignoreCase = false)
    {
        var pos = string.IsNullOrEmpty(marker)
            ? -1
            : text.IndexOf(marker, _Comparison(ignoreCase));
        return pos >= 0
            ? text[(pos + marker.Length)..]
            : text;
    }

    public static string AfterLast(
        string text,
        string marker,
        bool ignoreCase = false)
    {
        var pos = string.IsNullOrEmpty(marker)
            ? -1
            : text.LastIndexOf(marker, _Comparison(ignoreCase));
        return pos >= 0
            ? text[(pos + marker.Length)..]
            : text;
    }

    public static string Between(
        string text,
        string after,
        string before,
        bool ignoreCase = false)
    {
        var comparison = _Comparison(ignoreCase);
        var startPos = string.IsNullOrEmpty(after)
            ? -1
            : text.LastIndexOf(after, comparison);
        startPos = startPos >= 0
            ? startPos + after.Length
            : 0;

        var endPos = string.IsNullOrEmpty(before)
            ? -1
            : text.IndexOf(before, startPos, comparison);
        if (endPos >= 0)
            return text[startPos..endPos];

        return startPos > 0
            ? text[startPos..]
            : text;
    }

    private static StringComparison _Comparison(bool ignoreCase)
    {
        return ignoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}