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

    public async void Output(bool isHandAnalyze, List<string>? extraFiles)
    {
        try
        {
            await CrashPresenter.OutputAsync(_context, isHandAnalyze, extraFiles);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Crash", "展示崩溃报告失败");
        }
    }
}

internal static class CrashPresenter
{
    private static readonly CrashResultFormatter Formatter = new();
    private static readonly CrashReportExporter Exporter = new();

    public static async Task OutputAsync(
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

        var selectedButton = await UiInteraction.MessageAsync(
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
                await ExportReportAsync(context, extraFiles);
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
        PageRouter.Navigate(PageIds.InstanceSetup, SubPageIds.VersionInstall, instance);
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

    private static async Task ExportReportAsync(
        CrashAnalysisContext context,
        List<string>? extraFiles)
    {
        string? fileAddress = null;
        try
        {
            fileAddress = await SelectReportSavePathAsync();
            if (string.IsNullOrEmpty(fileAddress)) return;

            CrashReportExporter.Export(context, fileAddress, extraFiles);
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
            await UiInteraction.MessageAsync(
                message,
                Lang.Text("Crash.Export.Failed.Title"),
                Lang.Text("Common.Action.Confirm"),
                Lang.Text("Crash.Export.Failed.CopyDetails"),
                button2Action: () => copyPressed = true);
            if (copyPressed && ApplicationRuntime.GetService<IClipboardService>() is { } clipboard)
                await clipboard.SetTextAsync(message);
        }
    }

    private static string CreateExportFailureMessage(string? targetZipPath, Exception exception)
    {
        var summary = string.IsNullOrWhiteSpace(targetZipPath)
            ? Lang.Text("Crash.Export.Failed.MessageWithoutPath")
            : Lang.Text("Crash.Export.Failed.Message", targetZipPath);
        return ExceptionDetails.ComposeForDialog(summary, exception);
    }

    private static Task<string> SelectReportSavePathAsync()
    {
        return SystemDialogs.SelectSaveFileAsync(
            Lang.Text("Crash.Report.SaveDialog.Title"),
            GetDefaultReportFileName(),
            Lang.Text("Crash.Report.SaveDialog.Filter"));
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