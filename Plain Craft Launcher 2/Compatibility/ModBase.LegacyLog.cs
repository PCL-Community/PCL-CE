using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualBasic;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;

namespace PCL;

public static partial class ModBase
{
    #region Debug

    public static bool modeDebug = false;

    // Log
    public enum LogLevel
    {
        /// <summary>
        ///     不提示，只记录日志。
        /// </summary>
        Normal = 0,

        /// <summary>
        ///     只提示开发者。
        /// </summary>
        Developer = 1,

        /// <summary>
        ///     只提示开发者与调试模式用户。
        /// </summary>
        Debug = 2,

        /// <summary>
        ///     弹出提示所有用户。
        /// </summary>
        Hint = 3,

        /// <summary>
        ///     弹窗，不要求反馈。
        /// </summary>
        Msgbox = 4,

        /// <summary>
        ///     弹窗，要求反馈。
        /// </summary>
        Feedback = 5,

        /// <summary>
        ///     弹出 Windows 原生弹窗，要求反馈。在无法保证 WPF 窗口能正常运行时使用此级别。
        ///     在第二次触发后会直接结束程序。
        /// </summary>
        Critical = 6
    }

    private static bool isCriticalErrorTriggered;

    /// <summary>
    ///     输出 Log。
    /// </summary>
    /// <param name="title">如果要求弹窗，指定弹窗的标题。</param>
    public static void Log(
        string text,
        LogLevel level = LogLevel.Normal,
        string? title = null,
        string? userSummary = null)
    {
        // On Error Resume Next
        // 放在最后会导致无法显示极端错误下的弹窗（如无法写入日志文件）
        // 处理错误会导致再次调用 Log() 导致无限循环

        // 输出日志
        if (new[] { LogLevel.Msgbox, LogLevel.Hint }.Contains(level))
            LogWrapper.Warn(text);
        else
            switch (level)
            {
                case LogLevel.Feedback:
                    LogWrapper.Error(text);
                    break;
                case LogLevel.Critical:
                    LogWrapper.Fatal(text);
                    break;
                case LogLevel.Debug:
                    LogWrapper.Debug(text);
                    break;
                case LogLevel.Developer:
                    LogWrapper.Trace(text);
                    break;
                default:
                    LogWrapper.Info(text);
                    break;
            }

        if (isProgramEnded || level == LogLevel.Normal)
            return;

        var userDetails = text.RegexReplace(@"\[[^\]]+?\] ", "");
        var userMessage = string.IsNullOrWhiteSpace(userSummary)
            ? Lang.Text("SystemDialog.Error.UserVisible.Message", userDetails)
            : userSummary;
        var dialogTitle = _GetUserDialogTitle(title);

        switch (level)
        {
            case LogLevel.Developer:
                break;

            case LogLevel.Debug:
                if (modeDebug)
                    HintService.Hint("[调试模式] " + text, HintType.Info, false);
                break;

            case LogLevel.Hint:
                HintService.Hint(userMessage, HintType.Error, false);
                break;

            case LogLevel.Msgbox:
                ModMain.MyMsgBox(userMessage, dialogTitle, isWarn: true);
                break;

            case LogLevel.Feedback:
                _ShowFeedbackPrompt(userMessage, dialogTitle, false);
                break;

            case LogLevel.Critical:
                _ShowFeedbackPrompt(userMessage, dialogTitle, true);
                break;
        }
    }

    /// <summary>
    ///     输出错误信息。
    /// </summary>
    /// <param name="desc">错误描述，仅用于日志和错误详情。</param>
    /// <param name="userSummary">可选的本地化用户摘要；不会写入日志。</param>
    public static void Log(
        Exception ex,
        string desc,
        LogLevel level = LogLevel.Debug,
        string? title = null,
        string? userSummary = null)
    {
        // On Error Resume Next
        if (ex is ThreadInterruptedException)
            return;

        // 输出日志
        if (new[] { LogLevel.Msgbox, LogLevel.Hint }.Contains(level))
            LogWrapper.Warn(ex, desc);
        else
            switch (level)
            {
                case LogLevel.Feedback:
                    LogWrapper.Error(ex, desc);
                    break;
                case LogLevel.Critical:
                    LogWrapper.Fatal(ex, desc);
                    break;
                case LogLevel.Debug:
                    LogWrapper.Debug($"{desc}:{ex}");
                    break;
                case LogLevel.Developer:
                    LogWrapper.Trace($"{desc}:{ex}");
                    break;
                default:
                    LogWrapper.Error(ex, desc);
                    break;
            }

        if (isProgramEnded)
            return;

        var userMessage = _GetUserExceptionMessage(desc, ex, userSummary);
        var dialogTitle = _GetUserDialogTitle(title);

        switch (level)
        {
            case LogLevel.Normal or LogLevel.Developer:
                break;

            case LogLevel.Debug:
                if (modeDebug)
                    HintService.Hint("[调试模式] " + desc + "：" + ex, HintType.Info, false);
                break;

            case LogLevel.Hint:
                HintService.Hint(userMessage, HintType.Error, false);
                break;

            case LogLevel.Msgbox:
                ModMain.MyMsgBox(userMessage, dialogTitle, isWarn: true);
                break;

            case LogLevel.Feedback:
                _ShowFeedbackPrompt(userMessage, dialogTitle, false);
                break;

            case LogLevel.Critical:
                _ShowFeedbackPrompt(userMessage, dialogTitle, true);
                break;
        }
    }

    private static string _GetUserDialogTitle(string? title)
    {
        return string.IsNullOrWhiteSpace(title)
            ? Lang.Text("SystemDialog.Error.Title")
            : title;
    }

    private static string _GetUserExceptionMessage(
        string desc,
        Exception ex,
        string? userSummary)
    {
        if (!string.IsNullOrWhiteSpace(userSummary))
            return ExceptionDetails.Compose(userSummary, ex);

        return ex.GetType() == typeof(Win32Exception)
            ? Lang.Text("SystemDialog.Error.OperationFailed.RuntimeMessage", desc, ex.ToString())
            : Lang.Text("SystemDialog.Error.OperationFailed.Message", desc, ex.ToString());
    }

    private static void _ShowFeedbackPrompt(
        string userMessage,
        string title,
        bool isCritical)
    {
        switch (isCritical)
        {
            case true when isCriticalErrorTriggered:
                FormMain.EndProgramForce(ProcessReturnValues.Exception);
                return;
            case true:
                isCriticalErrorTriggered = true;
                break;
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

    public static string Base64Decode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var decodedBytes = Convert.FromBase64String(text);
        return Encoding.UTF8.GetString(decodedBytes);
    }

    public static string Base64Encode(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return Convert.ToBase64String(bytes);
    }

    public static string Base64Encode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes);
    }

    // 反馈
    public static void Feedback(bool showMsgbox = true, bool forceOpenLog = false)
    {
        // On Error Resume Next
        FeedbackInfo();
        var currentDate = DateTime.Now.ToString("yyyy-M-dd", CultureInfo.InvariantCulture);

        if (forceOpenLog || (showMsgbox &&
                             ModMain.MyMsgBox(
                                 Lang.Text("Setup.Feedback.Reminder.Message", currentDate),
                                 Lang.Text("Setup.Feedback.Reminder.Title"),
                                 Lang.Text("Common.Action.OpenFolder"),
                                 Lang.Text("Setup.Feedback.Reminder.NotNeeded")) ==
                             1)) OpenExplorer(exePath + @"PCL\Log\");
        OpenWebsite("https://github.com/PCL-Community/PCL2-CE/issues/");
    }

    public static bool CanFeedback(bool showHint)
    {
        var stat = UpdateManager.GetVersionStatus();
        if (stat == UpdateEnums.VersionStatus.Latest) return true;

        if (!showHint) return false;

        if (ModMain.MyMsgBox(
                stat == UpdateEnums.VersionStatus.NotLatest
                    ? Lang.Text("Setup.Feedback.Unavailable.NotLatest.Message")
                    : Lang.Text("Setup.Feedback.Unavailable.CheckFailed.Message"),
                Lang.Text("Setup.Feedback.Unavailable.Title"),
                stat == UpdateEnums.VersionStatus.NotLatest
                    ? Lang.Text("Setup.Feedback.Unavailable.NotLatest.Action")
                    : Lang.Text("Setup.Feedback.Unavailable.CheckFailed.Action"),
                Lang.Text("Common.Action.Cancel")) == 1)
            ModMain.frmMain.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupUpdate);

        return false;
    }

    /// <summary>
    ///     在日志中输出系统诊断信息。
    /// </summary>
    public static void FeedbackInfo()
    {
        try
        {
            // Get system memory info
            var phyRam = KernelInterop.GetPhysicalMemoryBytes();

            // Calculate memory and DPI scale
            var availableMb = phyRam.Available / 1024 / 1024;
            var totalMb = phyRam.Total / 1024 / 1024;
            var dpiScale = Math.Round(dpi / 96.0, 2);

            // Build diagnostic information string
            var info = $"""
                        [System] Diagnostic Information:
                        OS: {RuntimeInformation.OSDescription} (32-bit: {SystemInfo.Is32BitSystem})
                        Memory: {availableMb} MB / {totalMb} MB
                        DPI: {dpi} ({dpiScale * 100}%)
                        MC Folder: {ModFolder.mcFolderSelected ?? "Nothing"}
                        Executable Path: {exePath}
                        """;

            LogWrapper.Info(info);
        }
        catch (Exception ex)
        {
            // Basic fail-safe to replace "On Error Resume Next"
            LogWrapper.Error(ex, "Failed to collect feedback information");
        }
    }

    // 断言
    public static void DebugAssert(bool exp)
    {
        if (!exp)
            throw new Exception("断言命中");
    }

    // 获取当前的堆栈信息
    public static string GetStackTrace()
    {
        var stack = new StackTrace();
        return stack.GetFrames().Skip(1).Select(f => f.GetMethod())
            .Select(f => f.Name + "(" + f.GetParameters().Select(p => p.ToString()).ToList().Join(", ") + ") - " +
                         f.Module).ToList().Join("\r\n")
            .Replace("\r\n" + "\r\n", "\r\n");
    }

    #endregion
}