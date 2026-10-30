using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualBasic;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;

namespace PCL;

public static partial class Common
{
    public enum LogLevel
    {
        /// <summary>不提示，只记录日志。</summary>
        Normal = 0,

        /// <summary>只提示开发者。</summary>
        Developer = 1,

        /// <summary>只提示开发者与调试模式用户。</summary>
        Debug = 2,

        /// <summary>弹出提示所有用户。</summary>
        Hint = 3,

        /// <summary>弹窗，不要求反馈。</summary>
        Msgbox = 4,

        /// <summary>弹窗，要求反馈。</summary>
        Feedback = 5,

        /// <summary>
        ///     弹出 Windows 原生弹窗，要求反馈。
        ///     在无法保证 WPF 窗口能正常运行时使用。
        ///     第二次触发后直接结束程序。
        /// </summary>
        Critical = 6
    }

    public static bool modeDebug = false;

    private static bool _isCriticalErrorTriggered;

    /// <summary>
    ///     输出日志，并根据日志级别决定是否提示用户。
    /// </summary>
    /// <param name="title">弹窗标题。</param>
    public static void Log(
        string text,
        LogLevel level = LogLevel.Normal,
        string? title = null,
        string? userSummary = null)
    {
        switch (level)
        {
            case LogLevel.Hint or LogLevel.Msgbox:
                LogWrapper.Warn(text);
                break;

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

        if (!_ShouldShowLogNotification(level))
            return;

        if (level == LogLevel.Debug)
        {
            HintService.Hint($"[Debug] {text}", HintType.Info, false);
            return;
        }

        var userDetails = text.RegexReplace(@"\[[^\]]+?\] ", "");
        var userMessage = string.IsNullOrWhiteSpace(userSummary)
            ? Lang.Text("SystemDialog.Error.UserVisible.Message", userDetails)
            : userSummary;

        _ShowLogNotification(level, userMessage, _GetUserDialogTitle(title));
    }

    /// <summary>
    ///     输出异常日志，并根据日志级别决定是否提示用户。
    /// </summary>
    /// <param name="desc">错误描述，仅用于日志和错误详情。</param>
    /// <param name="userSummary">可选的本地化用户摘要，不会写入日志。</param>
    public static void Log(
        Exception ex,
        string desc,
        LogLevel level = LogLevel.Debug,
        string? title = null,
        string? userSummary = null)
    {
        if (ex is ThreadInterruptedException)
            return;

        switch (level)
        {
            case LogLevel.Hint or LogLevel.Msgbox:
                LogWrapper.Warn(ex, desc);
                break;

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

        if (!_ShouldShowLogNotification(level))
            return;

        if (level == LogLevel.Debug)
        {
            HintService.Hint($"[Debug] {desc}：{ex}", HintType.Info, false);
            return;
        }

        var userMessage = _GetUserExceptionMessage(desc, ex, userSummary);
        _ShowLogNotification(level, userMessage, _GetUserDialogTitle(title));
    }

    /// <summary>
    ///     判断当前日志是否需要向用户显示提示。
    /// </summary>
    private static bool _ShouldShowLogNotification(LogLevel level)
    {
        if (isProgramEnded)
            return false;

        return level switch
        {
            LogLevel.Debug => modeDebug,
            LogLevel.Hint or LogLevel.Msgbox or LogLevel.Feedback or LogLevel.Critical => true,
            _ => false
        };
    }

    /// <summary>
    ///     根据日志级别显示用户提示或弹窗。
    /// </summary>
    private static void _ShowLogNotification(
        LogLevel level,
        string message,
        string title)
    {
        switch (level)
        {
            case LogLevel.Hint:
                HintService.Hint(message, HintType.Error, false);
                break;

            case LogLevel.Msgbox:
                Launcher.MyMsgBox(message, title, isWarn: true);
                break;

            case LogLevel.Feedback:
                _ShowFeedbackPrompt(message, title, false);
                break;

            case LogLevel.Critical:
                _ShowFeedbackPrompt(message, title, true);
                break;

            case LogLevel.Normal:
            case LogLevel.Developer:
            case LogLevel.Debug:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }
    }

    /// <summary>
    ///     获取面向用户的弹窗标题。
    /// </summary>
    private static string _GetUserDialogTitle(string? title)
    {
        return string.IsNullOrWhiteSpace(title)
            ? Lang.Text("SystemDialog.Error.Title")
            : title;
    }

    /// <summary>
    ///     获取面向用户的异常信息。
    /// </summary>
    private static string _GetUserExceptionMessage(
        string desc,
        Exception ex,
        string? userSummary)
    {
        if (!string.IsNullOrWhiteSpace(userSummary))
            return ExceptionDetails.Compose(userSummary, ex);

        var messageKey = ex.GetType() == typeof(Win32Exception)
            ? "SystemDialog.Error.OperationFailed.RuntimeMessage"
            : "SystemDialog.Error.OperationFailed.Message";

        return Lang.Text(messageKey, desc, ex.ToString());
    }

    /// <summary>
    ///     显示错误反馈提示。
    /// </summary>
    private static void _ShowFeedbackPrompt(
        string userMessage,
        string title,
        bool isCritical)
    {
        if (isCritical)
        {
            if (_isCriticalErrorTriggered)
            {
                FormMain.EndProgramForce(ProcessReturnValues.Exception);
                return;
            }

            _isCriticalErrorTriggered = true;
        }

        if (!CanFeedback(false))
        {
            var message = Lang.Text(
                "Setup.Feedback.ErrorPrompt.Update.Message",
                userMessage);

            if (isCritical)
                Interaction.MsgBox(message, MsgBoxStyle.Critical, title);
            else
                Launcher.MyMsgBox(message, title, isWarn: true);

            return;
        }

        var feedbackMessage = Lang.Text(
            "Setup.Feedback.ErrorPrompt.Submit.Message",
            userMessage);

        var shouldSendFeedback = isCritical
            ? Interaction.MsgBox(
                feedbackMessage,
                MsgBoxStyle.Critical | MsgBoxStyle.YesNo,
                title)
            == MsgBoxResult.Yes
            : Launcher.MyMsgBox(
                feedbackMessage,
                title,
                Lang.Text("Setup.Feedback.ErrorPrompt.Submit.Action"),
                Lang.Text("Common.Action.Cancel"),
                isWarn: true)
            == 1;

        if (shouldSendFeedback)
            Feedback(false, true);
    }

    /// <summary>
    ///     将 Base64 字符串解码为 UTF-8 文本。
    /// </summary>
    public static string Base64Decode(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? ""
            : Encoding.UTF8.GetString(Convert.FromBase64String(text));
    }

    /// <summary>
    ///     将文本编码为 Base64。
    /// </summary>
    public static string Base64Encode(string text)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    ///     将字节数组编码为 Base64。
    /// </summary>
    public static string Base64Encode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    ///     打开反馈页面，并根据需要显示或打开日志文件夹。
    /// </summary>
    public static void Feedback(bool showMsgbox = true, bool forceOpenLog = false)
    {
        FeedbackInfo();

        var currentDate = DateTime.Now.ToString(
            "yyyy-M-dd",
            CultureInfo.InvariantCulture);

        var shouldOpenLogFolder = forceOpenLog
            || (showMsgbox
                && Launcher.MyMsgBox(
                    Lang.Text("Setup.Feedback.Reminder.Message", currentDate),
                    Lang.Text("Setup.Feedback.Reminder.Title"),
                    Lang.Text("Common.Action.OpenFolder"),
                    Lang.Text("Setup.Feedback.Reminder.NotNeeded"))
                == 1);
        if (shouldOpenLogFolder)
            OpenExplorer(ExePath + @"PCL\Log\");
        OpenWebsite("https://github.com/PCL-Community/PCL2-CE/issues/");
    }

    /// <summary>
    ///     检查当前版本是否支持提交反馈。
    /// </summary>
    public static bool CanFeedback(bool showHint)
    {
        var status = UpdateManager.GetVersionStatus();

        if (status == UpdateEnums.VersionStatus.Latest)
            return true;
        if (!showHint)
            return false;

        var (messageKey, actionKey) = status switch
        {
            UpdateEnums.VersionStatus.NotLatest => (
                "Setup.Feedback.Unavailable.NotLatest.Message",
                "Setup.Feedback.Unavailable.NotLatest.Action"),

            _ => (
                "Setup.Feedback.Unavailable.CheckFailed.Message",
                "Setup.Feedback.Unavailable.CheckFailed.Action")
        };

        var shouldOpenUpdatePage = Launcher.MyMsgBox(
                Lang.Text(messageKey),
                Lang.Text("Setup.Feedback.Unavailable.Title"),
                Lang.Text(actionKey),
                Lang.Text("Common.Action.Cancel"))
            == 1;

        if (shouldOpenUpdatePage)
            Launcher.frmMain?.PageChange(
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
            var memory = KernelInterop.GetPhysicalMemoryBytes();

            var availableMiB = memory.Available / 1024 / 1024;
            var totalMiB = memory.Total / 1024 / 1024;
            var dpiScale = Math.Round(Dpi / 96d, 2);

            var info = $"""
                        [System] Diagnostic Information:
                        OS: {RuntimeInformation.OSDescription} (32-bit: {SystemInfo.Is32BitSystem})
                        Memory: {availableMiB} MiB / {totalMiB} MiB
                        DPI: {Dpi} ({dpiScale * 100}%)
                        MC Folder: {GameFolders.mcFolderSelected ?? "Nothing"}
                        Executable Path: {ExePath}
                        """;

            LogWrapper.Info(info);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Failed to collect feedback information");
        }
    }

    /// <summary>
    ///     检查断言条件，不满足时抛出异常。
    /// </summary>
    public static void DebugAssert(bool exp)
    {
        if (!exp)
            throw new Exception("断言命中");
    }

    /// <summary>
    ///     获取当前调用堆栈，跳过本方法自身。
    /// </summary>
    public static string GetStackTrace()
    {
        var frames = new StackTrace().GetFrames();
        if (frames is null)
            return "";

        var lines = (
                from method in frames
                    .Skip(1)
                    .Select(frame => frame.GetMethod())
                    .OfType<MethodBase>()
                let parameters = string.Join(
                    ", ",
                    method.GetParameters().Select(p => p.ToString()))
                select $"{method.Name}({parameters}) - {method.Module}")
            .ToList();

        return string.Join("\r\n", lines);
    }
}
