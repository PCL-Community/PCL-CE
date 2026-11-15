namespace PCL;

/// <summary>将崩溃分析桥接到 app 的 CrashAnalyzer。</summary>
public sealed class WpfCrashAnalysisService : ICrashAnalysisService
{
    public void Analyze(
        int pid,
        McInstance version,
        string pathIndie,
        IReadOnlyList<string> logs,
        IReadOnlyList<string> outputPaths)
    {
        try
        {
            Thread.Sleep(2000);
            var analyzer = new CrashAnalyzer(pid);
            analyzer.Collect(pathIndie, [.. logs]);
            analyzer.Prepare();
            analyzer.Analyze(version);
            analyzer.Output(false, [.. outputPaths]);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "[Watcher] 崩溃分析失败");
        }
    }
}