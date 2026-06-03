namespace PCL.Core.Minecraft.CrashAnalysis;

public static partial class CrashReportSanitizer
{
    public static string Sanitize(string text, IReadOnlyList<string> sensitiveValues)
    {
        text = sensitiveValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Aggregate(text, (current, value) =>
                current.Replace(value, "<redacted>", StringComparison.OrdinalIgnoreCase));

        text = _AccessTokenRegex().Replace(text, "$1<redacted>");
        text = _UserPathRegex().Replace(text, "$1<user>$2");

        return text;
    }

    [GeneratedRegex(@"(?i)(--accessToken\s+|accessToken[=:]\s*)([^\s]+)")]
    private static partial Regex _AccessTokenRegex();

    [GeneratedRegex(@"(?i)([A-Z]:\\Users\\)([^\\\r\n]+)(\\)")]
    private static partial Regex _UserPathRegex();
}