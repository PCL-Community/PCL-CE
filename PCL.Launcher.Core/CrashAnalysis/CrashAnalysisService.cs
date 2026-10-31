namespace PCL;

/// <summary>崩溃分析服务，由 UI 程序集提供具体的 CrashAnalyzer 实现。</summary>
public interface ICrashAnalysisService
{
    void Analyze(
        int pid,
        McInstance version,
        string pathIndie,
        IReadOnlyList<string> logs,
        IReadOnlyList<string> outputPaths);
}

public static class CrashAnalysisService
{
    public static ICrashAnalysisService? Current { get; set; }
}