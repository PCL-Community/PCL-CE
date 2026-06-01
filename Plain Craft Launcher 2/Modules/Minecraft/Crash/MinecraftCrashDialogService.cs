using System.IO;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL;

public sealed class MinecraftCrashDialogService
{
    private readonly MinecraftCrashReportExportService _exportService = new();
    private readonly CrashResultLocalizer _localizer = new();
    private readonly CrashReportBuilder _reportBuilder = new();

    public static void Show(CrashAnalysisReport report, ModMinecraft.Instance? instance)
    {
        ModMain.frmMain?.ShowWindowToTop();

        var isLauncherLatest = true;
        try
        {
            isLauncherLatest = UpdateManager.GetVersionStatus() == UpdateEnums.VersionStatus.Latest;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "确认启动器更新失败", ModBase.LogLevel.Feedback);
        }

        var message = CrashResultLocalizer.Localize(report, new CrashResultLocalizeOptions
        {
            Mode = report.Request.Mode,
            IsLauncherLatest = isLauncherLatest
        });

        var secondaryAction = _SelectSecondaryAction(report, instance);
        var thirdAction = report.Actions
            .FirstOrDefault(static action => action.Kind == CrashSuggestedActionKind.ExportReport);
        var result = ModMain.MyMsgBox(
            message,
            report.Request.Mode == CrashAnalysisMode.Manual
                ? Lang.Text("Crash.Dialog.ManualTitle")
                : Lang.Text("Crash.Dialog.AutoTitle"),
            Lang.Text("Common.Action.Confirm"),
            secondaryAction is null ? "" : _GetActionText(secondaryAction.Kind),
            thirdAction is null ? "" : Lang.Text("Crash.Dialog.ExportReport"),
            button2Action: secondaryAction?.Kind == CrashSuggestedActionKind.ViewLog
                ? () => _ExecuteAction(secondaryAction, report, instance)
                : null);

        switch (result)
        {
            case 2 when secondaryAction is not null && secondaryAction.Kind != CrashSuggestedActionKind.ViewLog:
                _ExecuteAction(secondaryAction, report, instance);
                break;
            case 3 when thirdAction is not null:
                _ExecuteAction(thirdAction, report, instance);
                break;
        }
    }

    private static CrashSuggestedAction? _SelectSecondaryAction(
        CrashAnalysisReport report,
        ModMinecraft.Instance? instance)
    {
        if (instance is not null)
        {
            var modifyAction = report.Actions.FirstOrDefault(static action =>
                action.Kind == CrashSuggestedActionKind.OpenInstanceModifyPage);
            if (modifyAction is not null) return modifyAction;
        }

        return report.Actions.FirstOrDefault(static action => action.Kind == CrashSuggestedActionKind.ViewLog);
    }

    private static void _ExecuteAction(
        CrashSuggestedAction action,
        CrashAnalysisReport report,
        ModMinecraft.Instance? instance)
    {
        switch (action.Kind)
        {
            case CrashSuggestedActionKind.ViewLog:
                _OpenLog(action.TargetPath, report);
                break;
            case CrashSuggestedActionKind.ExportReport:
                var package = CrashReportBuilder.Build(report, new CrashReportBuildOptions
                {
                    UserNames = _CollectUserNames(report)
                });
                MinecraftCrashReportExportService.Export(package);
                break;
            case CrashSuggestedActionKind.OpenInstanceModifyPage:
                if (instance is null) return;
                PageInstanceLeft.instance = instance;
                ModBase.RunInUi(() =>
                    ModMain.frmMain?.PageChange(FormMain.PageType.InstanceSetup, FormMain.PageSubType.VersionInstall));
                break;
        }
    }

    private static void _OpenLog(string? targetPath, CrashAnalysisReport report)
    {
        if (!string.IsNullOrWhiteSpace(targetPath) && File.Exists(targetPath))
        {
            ModBase.ShellOnly(targetPath);
            return;
        }

        var text = report.Logs.PreferredOpenFile?.Content ?? report.Logs.GameText.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        var filePath = Path.Combine(ModBase.pathTemp, "Crash.txt");
        ModBase.WriteFile(filePath, text);
        ModBase.ShellOnly(filePath);
    }

    private static IReadOnlyList<string> _CollectUserNames(CrashAnalysisReport report)
    {
        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(report.Request.EnvironmentInfo?.AccountName))
            names.Add(report.Request.EnvironmentInfo.AccountName);

        return names;
    }

    private static string _GetActionText(CrashSuggestedActionKind kind)
    {
        return kind switch
        {
            CrashSuggestedActionKind.ViewLog => Lang.Text("Crash.Dialog.ViewLog"),
            CrashSuggestedActionKind.OpenInstanceModifyPage => Lang.Text("Crash.Dialog.GoToModify"),
            CrashSuggestedActionKind.OpenJavaSettings => Lang.Text("Crash.Dialog.OpenJavaSettings"),
            CrashSuggestedActionKind.OpenMemorySettings => Lang.Text("Crash.Dialog.OpenMemorySettings"),
            _ => ""
        };
    }
}