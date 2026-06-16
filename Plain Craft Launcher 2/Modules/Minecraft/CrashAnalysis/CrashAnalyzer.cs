using System.IO;
using PCL.Core.Logging;

namespace PCL;

public sealed class CrashAnalyzer
{
    private readonly CrashLogCollector _collector;
    private readonly CrashAnalysisContext _context;
    private readonly CrashDetector _detector;
    private readonly CrashDialogPresenter _dialogPresenter;
    private readonly CrashLogImporter _importer;
    private readonly CrashLogPreparer _preparer;

    public CrashAnalyzer(int uUid)
    {
        var tempFolder = ModMain.RequestTaskTempFolder();
        Directory.CreateDirectory(Path.Combine(tempFolder, "Temp"));
        Directory.CreateDirectory(Path.Combine(tempFolder, "Report"));

        _context = new CrashAnalysisContext(uUid, tempFolder);
        _collector = new CrashLogCollector(_context);
        _importer = new CrashLogImporter(_context);
        _preparer = new CrashLogPreparer(_context);
        _detector = new CrashDetector();
        _dialogPresenter = new CrashDialogPresenter(_context);

        LogWrapper.Info("Crash", $"崩溃分析暂存文件夹：{tempFolder}");
    }

    /// <summary>
    ///     将可用于分析的日志存储到崩溃分析上下文。
    /// </summary>
    /// <param name="latestLog">从 PCL 捕获到的最后 200 行程序输出。</param>
    public void Collect(string versionPathIndie, IList<string>? latestLog = null)
    {
        _collector.Collect(versionPathIndie, latestLog);
    }

    /// <summary>
    ///     从文件路径直接导入日志文件或崩溃报告压缩包。
    /// </summary>
    public void Import(string filePath)
    {
        _importer.Import(filePath);
    }

    /// <summary>
    ///     从原始日志中提取实际有用的文本片段并整理可用于生成报告的文件。
    /// </summary>
    public bool Prepare()
    {
        return _preparer.Prepare();
    }

    /// <summary>
    ///     根据整理后的日志与可能的实例信息分析崩溃原因。
    /// </summary>
    public void Analyze(McInstance? version = null)
    {
        _context.Instance = version;
        var logs = _context.PreparedLogs ??
                   throw new InvalidOperationException("Prepare must be called before Analyze.");
        _context.Result = _detector.Analyze(logs, version);
        _context.LogAll = logs.All;
    }

    /// <summary>
    ///     弹出崩溃弹窗，并指导导出崩溃报告。
    /// </summary>
    public void Output(bool isHandAnalyze, List<string>? extraFiles = null)
    {
        _dialogPresenter.Output(isHandAnalyze, extraFiles);
    }
}