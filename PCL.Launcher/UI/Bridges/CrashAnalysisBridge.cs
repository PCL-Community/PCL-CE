namespace PCL.UI.Bridges;

/// <summary>崩溃分析：收集日志、分析原因并弹出真实结果弹窗，可导出错误报告。</summary>
public sealed class CrashAnalysisBridge : ICrashAnalysisService
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
            var runner = new CrashRunner(pid);
            runner.Collect(pathIndie, [.. logs]);
            runner.Prepare();
            runner.Analyze(version);
            runner.Output(false, [.. outputPaths]);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "[Watcher] 崩溃分析失败");
        }
    }
}