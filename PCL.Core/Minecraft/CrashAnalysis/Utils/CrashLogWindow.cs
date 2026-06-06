namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     为页面预览和分析建立的日志窗口，避免 UI 直接渲染完整大日志。
/// </summary>
public sealed record CrashLogWindow
{
    public required CrashLogKind Kind { get; init; }
    public required string SourceName { get; init; }
    public string Head { get; init; } = string.Empty;
    public string Tail { get; init; } = string.Empty;
    public string ErrorWindow { get; init; } = string.Empty;

    public static CrashLogWindow Create(CrashLogDocument document)
    {
        var lines = document.Lines;
        var errors = lines
            .Select((line, index) => new { line, index })
            .Where(item => item.line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                           item.line.Contains("FATAL", StringComparison.OrdinalIgnoreCase) ||
                           item.line.Contains("Exception", StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .Select(item => item.line)
            .ToList();

        return new CrashLogWindow
        {
            Kind = document.Kind,
            SourceName = document.Name,
            Head = string.Join("\n", lines.Take(80)),
            Tail = string.Join("\n", lines.Skip(Math.Max(0, lines.Count - 160))),
            ErrorWindow = errors.Count == 0
                ? string.Join("\n", lines.Skip(Math.Max(0, lines.Count - 80)))
                : string.Join("\n", errors)
        };
    }
}