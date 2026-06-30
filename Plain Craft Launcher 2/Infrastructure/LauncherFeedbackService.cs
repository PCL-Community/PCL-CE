using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;

namespace PCL;

/// <summary>
///     PCL2 反馈入口与诊断信息收集。
/// </summary>
public static class LauncherFeedbackService
{
    public static void Feedback(bool showMsgbox = true, bool forceOpenLog = false)
    {
        FeedbackInfo();

        var currentDate = DateTime.Now.ToString("yyyy-M-dd", CultureInfo.InvariantCulture);
        var shouldOpenLogFolder = forceOpenLog ||
                                  (showMsgbox &&
                                   ModMain.MyMsgBox(
                                       Lang.Text("Setup.Feedback.Reminder.Message", currentDate),
                                       Lang.Text("Setup.Feedback.Reminder.Title"),
                                       Lang.Text("Common.Action.OpenFolder"),
                                       Lang.Text("Setup.Feedback.Reminder.NotNeeded")) == 1);

        if (shouldOpenLogFolder)
            LauncherProcess.OpenExplorer(
                LauncherPaths.ExecutableDirectoryWithSlash + @"PCL\Log\");

        LauncherProcess.OpenWebsite("https://github.com/PCL-Community/PCL2-CE/issues/");
    }

    public static bool CanFeedback(bool showHint)
    {
        var stat = UpdateManager.GetVersionStatus();

        if (stat == UpdateEnums.VersionStatus.Latest)
            return true;

        if (!showHint)
            return false;

        var message = stat == UpdateEnums.VersionStatus.NotLatest
            ? Lang.Text("Setup.Feedback.Unavailable.NotLatest.Message")
            : Lang.Text("Setup.Feedback.Unavailable.CheckFailed.Message");

        var action = stat == UpdateEnums.VersionStatus.NotLatest
            ? Lang.Text("Setup.Feedback.Unavailable.NotLatest.Action")
            : Lang.Text("Setup.Feedback.Unavailable.CheckFailed.Action");

        if (ModMain.MyMsgBox(
                message,
                Lang.Text("Setup.Feedback.Unavailable.Title"),
                action,
                Lang.Text("Common.Action.Cancel")) == 1)
            ModMain.frmMain.PageChange(
                FormMain.PageType.Setup,
                FormMain.PageSubType.SetupUpdate);

        return false;
    }

    /// <summary>
    ///     在日志中输出系统诊断信息。
    /// </summary>
    public static void FeedbackInfo()
    {
        try
        {
            var phyRam = KernelInterop.GetPhysicalMemoryBytes();
            var availableMb = phyRam.Available / 1024 / 1024;
            var totalMb = phyRam.Total / 1024 / 1024;
            var dpiScale = Math.Round(DpiUtils.Dpi / 96.0, 2);

            var info = $"""
                        [System] Diagnostic Information:
                        OS: {RuntimeInformation.OSDescription} (32-bit: {SystemInfo.Is32BitSystem})
                        Memory: {availableMb} MB / {totalMb} MB
                        DPI: {DpiUtils.Dpi} ({dpiScale * 100}%)
                        MC Folder: {ModFolder.mcFolderSelected ?? "Nothing"}
                        Executable Path: {LauncherPaths.ExecutableDirectoryWithSlash}
                        """;

            LogWrapper.Info(info);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Failed to collect feedback information");
        }
    }

    public static void ShowFeedbackPrompt(string userMessage, string title, bool isCritical)
    {
        if (isCritical && LauncherLog.MarkCriticalErrorTriggered())
        {
            FormMain.EndProgramForce(LauncherExitCode.Exception);
            return;
        }

        if (CanFeedback(false))
        {
            var message = Lang.Text("Setup.Feedback.ErrorPrompt.Submit.Message", userMessage);
            var shouldSend = isCritical
                ? Interaction.MsgBox(
                    message,
                    (MsgBoxStyle)((int)MsgBoxStyle.Critical + (int)MsgBoxStyle.YesNo),
                    title) == MsgBoxResult.Yes
                : ModMain.MyMsgBox(
                    message,
                    title,
                    Lang.Text("Setup.Feedback.ErrorPrompt.Submit.Action"),
                    Lang.Text("Common.Action.Cancel"),
                    isWarn: true) == 1;

            if (shouldSend)
                Feedback(false, true);

            return;
        }

        var updateMessage = Lang.Text("Setup.Feedback.ErrorPrompt.Update.Message", userMessage);

        if (isCritical)
            Interaction.MsgBox(updateMessage, MsgBoxStyle.Critical, title);
        else
            ModMain.MyMsgBox(updateMessage, title, isWarn: true);
    }
}