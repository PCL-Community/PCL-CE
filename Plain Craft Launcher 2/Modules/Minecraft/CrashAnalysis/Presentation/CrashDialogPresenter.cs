using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

internal sealed class CrashDialogPresenter(CrashAnalysisContext context)
{
    private readonly CrashResultFormatter _formatter = new();

    public void Output(
        bool isHandAnalyze,
        List<string>? extraFiles)
    {
        ModMain.frmMain!.ShowWindowToTop();

        // 弹窗显示在主界面上，先关掉可能还开着的独立窗口，避免它盖住弹窗
        CrashReportSession.CloseWindow();

        var crashContent = _formatter.Format(context, isHandAnalyze);

        var title = isHandAnalyze
            ? Lang.Text("Crash.Dialog.Title.Manual")
            : Lang.Text("Crash.Dialog.Title.Auto");

        var report = new CrashReportSnapshot(context, crashContent, title, isHandAnalyze, extraFiles);

        // 「收起」与「在独立窗口中打开」都需要在弹窗关闭后执行，
        // 因此这里只收集按钮，具体动作在弹窗返回后统一分发。
        var canDock = Config.Launch.CrashReportDock;

        var selectedButton = MsgBoxWrapper.ShowWithCustomButtons(
            crashContent.Text,
            title,
            MsgBoxTheme.Info,
            true,
            new MsgBoxButtonInfo(
                canDock ? Lang.Text("Crash.Dialog.Button.Dock") : Lang.Text("Common.Action.Confirm"),
                1),
            // 「打开日志」沿用既有行为：执行动作时不关闭弹窗，便于同时对照日志与报告
            new MsgBoxButtonInfo(_GetSecondButtonText(report), 2,
                report.ShowOpenDirectFile ? report.OpenDirectFile : null),
            new MsgBoxButtonInfo(_GetThirdButtonText(report), 3),
            new MsgBoxButtonInfo(canDock ? Lang.Text("Crash.Dialog.Button.OpenInWindow") : "", 4));

        switch (selectedButton)
        {
            case 1:
                if (canDock)
                    Collapse(report);
                break;

            case 2:
                if (report.ShowOpenInstanceSettings)
                    report.OpenInstanceSettings();
                else if (report.ShowOpenDirectFile)
                    report.OpenDirectFile();
                break;

            case 3:
                if (report.CanExportReport)
                    report.ExportReport();
                break;

            case 4:
                if (canDock)
                {
                    Collapse(report);
                    CrashReportSession.OpenWindow();
                }

                break;
        }
    }

    /// <summary>
    /// 收起错误报告：弹窗关闭后保留报告，并在右下角显示入口。
    /// </summary>
    private static void Collapse(CrashReportSnapshot report)
    {
        CrashReportSession.Collapse(report);
        HintWrapper.Show(Lang.Text("Crash.Dock.Hint"), HintTheme.Info);
    }

    private static string _GetSecondButtonText(CrashReportSnapshot report)
    {
        if (report.ShowOpenInstanceSettings)
            return Lang.Text("Crash.Dialog.Button.GoToModify");
        return report.ShowOpenDirectFile ? Lang.Text("Crash.Dialog.Button.OpenLog") : "";
    }

    private static string _GetThirdButtonText(CrashReportSnapshot report)
    {
        return report.CanExportReport ? Lang.Text("Crash.Dialog.Button.ExportReport") : "";
    }
}
