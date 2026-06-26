using System.Globalization;
using System.IO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI;

namespace PCL;

internal sealed class CrashDialogPresenter(CrashAnalysisContext context)
{
    private readonly CrashReportExporter _exporter = new();
    private readonly CrashResultFormatter _formatter = new();

    public void Output(
        bool isHandAnalyze,
        List<string>? extraFiles)
    {
        ModMain.frmMain.ShowWindowToTop();

        var resultText = _formatter.Format(context, isHandAnalyze);
        var directFile = context.DirectOpenFile;
        var isModLoaderIncompatible = _IsModLoaderIncompatible(resultText);

        var title = isHandAnalyze
            ? Lang.Text("Crash.Dialog.Title.Manual")
            : Lang.Text("Crash.Dialog.Title.Auto");

        var secondButtonText = _GetSecondButtonText(
            isHandAnalyze,
            directFile,
            isModLoaderIncompatible);

        var thirdButtonText = isHandAnalyze
            ? ""
            : Lang.Text("Crash.Dialog.Button.ExportReport");

        var secondButtonAction = _GetSecondButtonAction(
            isHandAnalyze,
            directFile,
            isModLoaderIncompatible);

        var selectedButton = MsgBoxWrapper.ShowWithCustomButtons(
            resultText,
            title,
            MsgBoxTheme.Info,
            true,
            new MsgBoxButtonInfo(Lang.Text("Common.Action.Confirm"), 1),
            new MsgBoxButtonInfo(secondButtonText, 2, secondButtonAction),
            new MsgBoxButtonInfo(thirdButtonText, 3));

        switch (selectedButton)
        {
            case 2:
                _OpenModLoaderInstallPage();
                break;

            case 3:
                _ExportReport(extraFiles);
                break;
        }
    }

    private bool _IsModLoaderIncompatible(string resultText)
    {
        return context.Instance is not null &&
               resultText.StartsWith(Lang.Text("Crash.Result.ModLoaderIncompatible.Prefix"));
    }

    private static string _GetSecondButtonText(
        bool isHandAnalyze,
        CrashLogEntry? directFile,
        bool isModLoaderIncompatible)
    {
        if (isHandAnalyze || directFile is null)
            return "";

        return isModLoaderIncompatible
            ? Lang.Text("Crash.Dialog.Button.GoToModify")
            : Lang.Text("Crash.Dialog.Button.OpenLog");
    }

    private static Action? _GetSecondButtonAction(
        bool isHandAnalyze,
        CrashLogEntry? directFile,
        bool isModLoaderIncompatible)
    {
        if (isHandAnalyze ||
            directFile is null ||
            isModLoaderIncompatible)
            return null;

        return () => _OpenDirectFile(directFile);
    }

    private void _OpenModLoaderInstallPage()
    {
        PageInstanceLeft.McInstance = context.Instance;

        ModBase.RunInUi(() => ModMain.frmMain.PageChange(
            FormMain.PageType.InstanceSetup,
            FormMain.PageSubType.VersionInstall));
    }

    private static void _OpenDirectFile(CrashLogEntry directFile)
    {
        if (File.Exists(directFile.FullPath))
        {
            Basics.OpenPath(directFile.FullPath);
            return;
        }

        var filePath = Path.Combine(Paths.Temp, "Crash.txt");

        CrashFileIo.WriteText(filePath, string.Join("\r\n", directFile.Lines));
        Basics.OpenPath(filePath);
    }

    private void _ExportReport(List<string>? extraFiles)
    {
        try
        {
            var fileAddress = _SelectReportSavePath();

            if (string.IsNullOrEmpty(fileAddress))
                return;

            _exporter.Export(context, fileAddress, extraFiles);

            HintWrapper.Show(
                Lang.Text("Crash.Report.Export.Success"),
                HintTheme.Success);

            Basics.OpenPath(Path.GetDirectoryName(fileAddress) ?? fileAddress);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Crash", "导出错误报告失败");
        }
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