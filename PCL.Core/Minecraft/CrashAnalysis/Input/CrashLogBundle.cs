namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     一次分析使用的日志集合，以及为 UI 和报告导出保留的日志窗口。
/// </summary>
public sealed record CrashLogBundle
{
    public IReadOnlyList<CrashLogDocument> Documents { get; init; } = [];
    public IReadOnlyList<CrashLogWindow> Windows { get; init; } = [];

    public bool HasUsefulLog => Documents.Any(static document =>
        !document.IsEmpty && document.AnalysisRole != CrashLogAnalysisRole.ReportOnly);

    public CrashLogDocument? PreferredOpenDocument
    {
        get
        {
            return FirstAnalyzed(CrashLogKind.MinecraftCrashReport)
                   ?? FirstAnalyzed(CrashLogKind.JavaFatalErrorLog)
                   ?? FirstAnalyzed(CrashLogKind.CapturedGameOutput)
                   ?? FirstAnalyzed(CrashLogKind.MinecraftLatestLog)
                   ?? FirstAnalyzed(CrashLogKind.MinecraftDebugLog)
                   ?? Documents.FirstOrDefault(static document =>
                       !document.IsEmpty && document.AnalysisRole != CrashLogAnalysisRole.ReportOnly)
                   ?? Documents.FirstOrDefault(static document => !document.IsEmpty);
        }
    }

    public CrashLogDocument? FirstAnalyzed(CrashLogKind kind)
    {
        return Documents.FirstOrDefault(document =>
            document.Kind == kind && document.AnalysisRole != CrashLogAnalysisRole.ReportOnly);
    }

    public IReadOnlyList<CrashLogDocument> AnalyzedDocuments()
    {
        return Documents.Where(static document => document.AnalysisRole != CrashLogAnalysisRole.ReportOnly).ToList();
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