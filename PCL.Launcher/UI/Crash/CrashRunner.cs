using System.Globalization;
using Avalonia.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Navigation;
using PCL.UI;

namespace PCL;

/// <summary>
///     崩溃分析的一次执行：收集日志、整理、检测原因，并把结果交给弹窗呈现。
///     内核（收集/整理/检测）位于 PCL.Launcher.Core，本类只负责流程编排与界面呈现。
/// </summary>
internal sealed class CrashRunner
{
    private readonly CrashLogCollector _collector;
    private readonly CrashAnalysisContext _context;
    private readonly CrashDetector _detector;
    private readonly CrashLogPreparer _preparer;

    public CrashRunner(int processId)
    {
        var tempFolder = Path.Combine(
            LauncherPaths.Temp,
            "TaskTemp",
            $"{LauncherCommon.GetUuid()}-{Random.Shared.Next(0, 1000000)}");
        Directory.CreateDirectory(tempFolder);
        Directory.CreateDirectory(Path.Combine(tempFolder, "Temp"));
        Directory.CreateDirectory(Path.Combine(tempFolder, "Report"));

        _context = new CrashAnalysisContext(processId, tempFolder);
        _collector = new CrashLogCollector(_context);
        _preparer = new CrashLogPreparer(_context);
        _detector = new CrashDetector();
        LogWrapper.Info("Crash", $"崩溃分析暂存文件夹：{tempFolder}");
    }

    public void Collect(string versionPathIndie, IList<string>? latestLog)
    {
        _collector.Collect(versionPathIndie, latestLog);
    }

    public void Import(string filePath)
    {
        var importer = new CrashLogImporter(_context);
        importer.Import(filePath);
        if (!Prepare())
            return;

        Analyze(InstanceList.McMcInstanceSelected);
        Output(true, null);
    }

    public bool Prepare()
    {
        return _preparer.Prepare();
    }

    public void Analyze(McInstance? version)
    {
        _context.Instance = version;
        var logs = _context.PreparedLogs
            ?? throw new InvalidOperationException("Prepare must be called before Analyze.");
        _context.Result = _detector.Analyze(logs, version);
        _context.LogAll = logs.All;
    }

    public void Output(bool isHandAnalyze, List<string>? extraFiles)
    {
        CrashPresenter.Output(_context, isHandAnalyze, extraFiles);
    }
}

/// <summary>崩溃结果的弹窗呈现与报告导出。</summary>
internal static class CrashPresenter
{
    private static readonly CrashResultFormatter Formatter = new();
    private static readonly CrashReportExporter Exporter = new();

    public static void Output(
        CrashAnalysisContext context,
        bool isHandAnalyze,
        List<string>? extraFiles)
    {
        BringMainWindowToFront();

        var crashContent = CrashResultFormatter.Format(context, isHandAnalyze);
        var directFile = context.DirectOpenFile;
        var openInstanceSettings = context.Instance is not null
            && crashContent.SuggestedAction
            == CrashSuggestedAction.OpenInstanceSettings;
        var title = isHandAnalyze
            ? Lang.Text("Crash.Dialog.Title.Manual")
            : Lang.Text("Crash.Dialog.Title.Auto");
        var secondButtonText = GetSecondButtonText(isHandAnalyze, directFile, openInstanceSettings);
        var thirdButtonText = isHandAnalyze
            ? ""
            : Lang.Text("Crash.Dialog.Button.ExportReport");

        var selectedButton = UiInteraction.Message(
            crashContent.Text,
            title,
            Lang.Text("Common.Action.Confirm"),
            secondButtonText,
            thirdButtonText);

        switch (selectedButton)
        {
            case 2:
                if (openInstanceSettings)
                    OpenModLoaderInstallPage(context.Instance!);
                else if (directFile is not null)
                    OpenDirectFile(directFile);
                break;
            case 3:
                ExportReport(context, extraFiles);
                break;
        }
    }

    private static string GetSecondButtonText(
        bool isHandAnalyze,
        CrashLogEntry? directFile,
        bool openInstanceSettings)
    {
        if (isHandAnalyze || directFile is null) return "";
        return openInstanceSettings
            ? Lang.Text("Crash.Dialog.Button.GoToModify")
            : Lang.Text("Crash.Dialog.Button.OpenLog");
    }

    private static void OpenModLoaderInstallPage(McInstance instance)
    {
        PageRouter.Navigate(PageType.InstanceSetup, PageSubType.VersionInstall, instance);
    }

    private static void OpenDirectFile(CrashLogEntry directFile)
    {
        if (File.Exists(directFile.FullPath))
        {
            Basics.OpenPath(directFile.FullPath);
            return;
        }

        var filePath = Path.Combine(LauncherPaths.Temp, "Crash.txt");
        CrashFileIo.WriteText(filePath, string.Join("\r\n", directFile.Lines));
        Basics.OpenPath(filePath);
    }

    private static void ExportReport(
        CrashAnalysisContext context,
        List<string>? extraFiles)
    {
        string? fileAddress = null;
        try
        {
            fileAddress = SelectReportSavePath();
            if (string.IsNullOrEmpty(fileAddress)) return;

            Exporter.Export(context, fileAddress, extraFiles);
            UiInteraction.Hint(
                Lang.Text("Crash.Report.Export.Success"),
                NotificationType.Success);
            Basics.OpenPath(Path.GetDirectoryName(fileAddress) ?? fileAddress);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Crash", "导出错误报告失败");
            var message = CreateExportFailureMessage(fileAddress, ex);
            var copyPressed = false;
            UiInteraction.Message(
                message,
                Lang.Text("Crash.Export.Failed.Title"),
                Lang.Text("Common.Action.Confirm"),
                Lang.Text("Crash.Export.Failed.CopyDetails"),
                button2Action: () => copyPressed = true);
            if (copyPressed)
                PlatformServices.Clipboard?.SetText(message);
        }
    }

    private static string CreateExportFailureMessage(string? targetZipPath, Exception exception)
    {
        var summary = string.IsNullOrWhiteSpace(targetZipPath)
            ? Lang.Text("Crash.Export.Failed.MessageWithoutPath")
            : Lang.Text("Crash.Export.Failed.Message", targetZipPath);
        return ExceptionDetails.Compose(summary, exception);
    }

    private static string? SelectReportSavePath()
    {
        string? fileAddress = null;
        LauncherCommon.RunInUiWait(() => fileAddress = SystemDialogs.SelectSaveFile(
            Lang.Text("Crash.Report.SaveDialog.Title"),
            GetDefaultReportFileName(),
            Lang.Text("Crash.Report.SaveDialog.Filter")));
        return fileAddress;
    }

    private static string GetDefaultReportFileName()
    {
        var time = DateTime
            .Now
            .ToString("G", CultureInfo.InvariantCulture)
            .Replace("/", "-")
            .Replace(":", ".")
            .Replace(" ", "_");
        return Lang.Text("Crash.Report.SaveDialog.DefaultFileName", time);
    }

    private static void BringMainWindowToFront()
    {
        LauncherCommon.RunInUi(() =>
        {
            var window = UiInteraction.MainWindow;
            if (window is null) return;
            if (!window.IsVisible) window.Show();
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;
            window.Activate();
        });
    }
}