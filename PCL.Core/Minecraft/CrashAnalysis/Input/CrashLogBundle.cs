namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     一次分析使用的日志集合，以及为 UI 和报告导出保留的日志窗口。
/// </summary>
public sealed record CrashLogBundle
{
    public IReadOnlyList<CrashLogDocument> Documents { get; init; } = [];
    public IReadOnlyList<CrashLogWindow> Windows { get; init; } = [];

    public bool HasUsefulLog => Documents.Any(static document => !document.IsEmpty);

    public CrashLogDocument? PreferredOpenDocument
    {
        get
        {
            return First(CrashLogKind.MinecraftCrashReport)
                   ?? First(CrashLogKind.CapturedGameOutput)
                   ?? First(CrashLogKind.MinecraftLatestLog)
                   ?? First(CrashLogKind.MinecraftDebugLog)
                   ?? First(CrashLogKind.JavaFatalErrorLog)
                   ?? Documents.FirstOrDefault(static document => !document.IsEmpty);
        }
    }

    public CrashLogDocument? First(CrashLogKind kind)
    {
        return Documents.FirstOrDefault(document => document.Kind == kind);
    }

    public IReadOnlyList<CrashLogDocument> OfKind(CrashLogKind kind)
    {
        return Documents.Where(document => document.Kind == kind).ToList();
    }
}