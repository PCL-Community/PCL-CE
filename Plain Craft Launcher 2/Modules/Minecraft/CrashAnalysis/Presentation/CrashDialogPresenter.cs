using System.Globalization;
using System.IO;
using PCL.Core.App.Localization;
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

        var selectedButton = ModMain.MyMsgBox(
            resultText,
            title,
            Lang.Text("Common.Action.Confirm"),
            secondButtonText,
            thirdButtonText,
            button2Action: secondButtonAction);

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
            ModBase.ShellOnly(directFile.FullPath);
            return;
        }

        var filePath = Path.Combine(ModBase.pathTemp, "Crash.txt");

        ModBase.WriteFile(filePath, directFile.Lines.Join("\r\n"));
        ModBase.ShellOnly(filePath);
    }

    private void _ExportReport(List<string>? extraFiles)
    {
        try
        {
            var fileAddress = _SelectReportSavePath();

            if (string.IsNullOrEmpty(fileAddress))
                return;

            _exporter.Export(context, fileAddress, extraFiles);

            ModMain.Hint(
                Lang.Text("Crash.Report.Export.Success"),
                ModMain.HintType.Finish);

            ModBase.OpenExplorer(fileAddress);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "导出错误报告失败", ModBase.LogLevel.Feedback);
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