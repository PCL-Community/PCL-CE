using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI;
using System.Globalization;
using System.IO;

namespace PCL;

/// <summary>
/// 一次崩溃分析的展示与操作快照。
/// 它只描述「要展示什么」和「能做什么」，不依赖弹窗与窗体，
/// 因此错误报告弹窗、右下角入口与独立窗口可以共用同一份实现。
/// </summary>
internal sealed class CrashReportSnapshot
{
    private readonly CrashAnalysisContext _context;
    private readonly CrashReportExporter _exporter = new();
    private readonly IReadOnlyList<string> _extraFiles;

    public CrashReportSnapshot(
        CrashAnalysisContext context,
        CrashDialogContent content,
        string title,
        bool isHandAnalyze,
        IReadOnlyList<string>? extraFiles)
    {
        _context = context;
        _extraFiles = extraFiles ?? [];
        Title = title;
        Text = content.Text;
        EntryLabel = _CreateEntryLabel(context);
        DirectFile = isHandAnalyze ? null : context.DirectOpenFile;
        CanExportReport = !isHandAnalyze;
        ShowOpenInstanceSettings = DirectFile is not null &&
                                   context.Instance is not null &&
                                   content.SuggestedAction == CrashSuggestedAction.OpenInstanceSettings;
        ShowOpenDirectFile = DirectFile is not null && !ShowOpenInstanceSettings;
    }

    /// <summary>
    /// 报告标题，与弹窗标题一致。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 报告正文，与弹窗内容一致。
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 收起入口上显示的名称，格式为「游戏版本名-时间」。
    /// </summary>
    public string EntryLabel { get; }

    /// <summary>
    /// 是否可以在弹窗上提供「导出错误报告」。手动分析不提供该操作。
    /// </summary>
    public bool CanExportReport { get; }

    /// <summary>
    /// 是否可以在弹窗上提供「打开日志」。
    /// </summary>
    public bool ShowOpenDirectFile { get; }

    /// <summary>
    /// 是否可以在弹窗上提供「前往修改」。
    /// </summary>
    public bool ShowOpenInstanceSettings { get; }

    private CrashLogEntry? DirectFile { get; }

    /// <summary>
    ///     生成收起入口上的名称。
    ///     自动分析时使用游戏版本名，手动分析时没有实例信息，退回报告标题。
    /// </summary>
    private static string _CreateEntryLabel(CrashAnalysisContext context)
    {
        var instanceName = context.Instance?.Name;
        if (string.IsNullOrWhiteSpace(instanceName))
            instanceName = Lang.Text("Crash.Report.Window.Title");

        var time = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);

        try
        {
            return Lang.Text("Crash.Dock.Entry", instanceName, time);
        }
        catch (FormatException ex)
        {
            // 语言文件中的格式字符串有误时退回简单拼接，不影响报告本身
            LogWrapper.Error(ex, "Crash", "格式化错误报告入口名称失败");
            return $"{instanceName}-{time}";
        }
    }

    /// <summary>
    /// 把报告正文复制到剪贴板。
    /// </summary>
    public void CopyReport()
    {
        ModBase.ClipboardSet(Text);
    }

    /// <summary>
    /// 打开与崩溃直接相关的日志文件。
    /// </summary>
    public void OpenDirectFile()
    {
        if (DirectFile is null)
            return;

        if (File.Exists(DirectFile.FullPath))
        {
            Basics.OpenPath(DirectFile.FullPath);
            return;
        }

        var filePath = Path.Combine(Paths.Temp, "Crash.txt");

        CrashFileIo.WriteText(filePath, string.Join("\r\n", DirectFile.Lines));
        Basics.OpenPath(filePath);
    }

    /// <summary>
    /// 跳转到实例的模组管理页面。
    /// </summary>
    public void OpenInstanceSettings()
    {
        if (_context.Instance is null)
            return;

        PageInstanceLeft.McInstance = _context.Instance;

        ModBase.RunInUi(() => ModMain.frmMain!.PageChange(
            FormMain.PageType.InstanceSetup,
            FormMain.PageSubType.VersionInstall));
    }

    /// <summary>
    /// 导出错误报告压缩包。
    /// </summary>
    public void ExportReport()
    {
        string? fileAddress = null;

        try
        {
            fileAddress = _SelectReportSavePath();

            if (string.IsNullOrEmpty(fileAddress))
                return;

            _exporter.Export(_context, fileAddress, _extraFiles);

            HintWrapper.Show(
                Lang.Text("Crash.Report.Export.Success"),
                HintTheme.Success);

            Basics.OpenPath(Path.GetDirectoryName(fileAddress) ?? fileAddress);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Crash", "导出错误报告失败");

            var message = _CreateExportFailureMessage(fileAddress, ex);
            MsgBoxWrapper.ShowWithCustomButtons(
                message,
                Lang.Text("Crash.Export.Failed.Title"),
                MsgBoxTheme.Error,
                false,
                new MsgBoxButtonInfo(Lang.Text("Common.Action.Confirm"), 1),
                new MsgBoxButtonInfo(
                    Lang.Text("Crash.Export.Failed.CopyDetails"),
                    2,
                    () => ModBase.ClipboardSet(message, false)));
        }
    }

    private static string _CreateExportFailureMessage(
        string? targetZipPath,
        Exception exception)
    {
        var summary = string.IsNullOrWhiteSpace(targetZipPath)
            ? Lang.Text("Crash.Export.Failed.MessageWithoutPath")
            : Lang.Text("Crash.Export.Failed.Message", targetZipPath);

        return ExceptionDetails.Compose(summary, exception);
    }

    private static string? _SelectReportSavePath()
    {
        string? fileAddress = null;

        ModBase.RunInUiWait(() => fileAddress = SystemDialogs.SelectSaveFile(
            Lang.Text("Crash.Report.SaveDialog.Title"),
            _GetDefaultReportFileName(),
            Lang.Text("Crash.Report.SaveDialog.Filter")));

        return fileAddress;
    }

    private static string _GetDefaultReportFileName()
    {
        var time = DateTime.Now
            .ToString("G", CultureInfo.InvariantCulture)
            .Replace("/", "-")
            .Replace(":", ".")
            .Replace(" ", "_");

        return Lang.Text("Crash.Report.SaveDialog.DefaultFileName", time);
    }
}
