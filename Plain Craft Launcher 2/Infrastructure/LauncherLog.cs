using System.ComponentModel;
using System.Diagnostics;
using PCL.Core.App.Localization;
using PCL.Core.Logging;

namespace PCL;

/// <summary>
///     日志与用户错误提示门面。
/// </summary>
public static class LauncherLog
{
    private static bool _isCriticalErrorTriggered;

    public static void Log(
        string text,
        LauncherLogLevel level = LauncherLogLevel.Normal,
        string? title = null,
        string? userSummary = null)
    {
        WriteTextLog(text, level);

        if (LauncherRuntime.IsProgramEnded || level is LauncherLogLevel.Normal or LauncherLogLevel.Developer)
            return;

        if (level == LauncherLogLevel.Debug)
        {
            ShowDebugHint(text);
            return;
        }

        var userDetails = text.RegexReplace(@"\[[^\]]+?\] ", "");
        var userMessage = string.IsNullOrWhiteSpace(userSummary)
            ? Lang.Text("SystemDialog.Error.UserVisible.Message", userDetails)
            : userSummary;

        ShowUserNotification(level, userMessage, GetUserDialogTitle(title));
    }

    public static void Log(
        Exception ex,
        string desc,
        LauncherLogLevel level = LauncherLogLevel.Debug,
        string? title = null,
        string? userSummary = null)
    {
        if (ex is ThreadInterruptedException)
            return;

        WriteExceptionLog(ex, desc, level);

        if (LauncherRuntime.IsProgramEnded || level is LauncherLogLevel.Normal or LauncherLogLevel.Developer)
            return;

        if (level == LauncherLogLevel.Debug)
        {
            ShowDebugHint($"{desc}：{ex}");
            return;
        }

        var userMessage = GetUserExceptionMessage(desc, ex, userSummary);
        ShowUserNotification(level, userMessage, GetUserDialogTitle(title));
    }

    public static bool MarkCriticalErrorTriggered()
    {
        if (_isCriticalErrorTriggered)
            return true;

        _isCriticalErrorTriggered = true;
        return false;
    }

    public static void DebugAssert(bool exp)
    {
        if (!exp)
            throw new Exception("断言命中");
    }

    public static string GetStackTrace()
    {
        var stack = new StackTrace();

        return string.Join(
                "\r\n",
                stack.GetFrames()
                    .Skip(1)
                    .Select(frame => frame.GetMethod())
                    .Where(method => method is not null)
                    .Select(method =>
                        $"{method!.Name}({string.Join(", ", method.GetParameters().Select(p => p.ToString()))}) - {method.Module}"))
            .Replace("\r\n\r\n", "\r\n");
    }

    private static void WriteTextLog(string text, LauncherLogLevel level)
    {
        switch (level)
        {
            case LauncherLogLevel.Msgbox or LauncherLogLevel.Hint:
                LogWrapper.Warn(text);
                break;

            case LauncherLogLevel.Feedback:
                LogWrapper.Error(text);
                break;

            case LauncherLogLevel.Critical:
                LogWrapper.Fatal(text);
                break;

            case LauncherLogLevel.Debug:
                LogWrapper.Debug(text);
                break;

            case LauncherLogLevel.Developer:
                LogWrapper.Trace(text);
                break;

            case LauncherLogLevel.Normal:
            default:
                LogWrapper.Info(text);
                break;
        }
    }

    private static void WriteExceptionLog(Exception ex, string desc, LauncherLogLevel level)
    {
        switch (level)
        {
            case LauncherLogLevel.Msgbox or LauncherLogLevel.Hint:
                LogWrapper.Warn(ex, desc);
                break;

            case LauncherLogLevel.Feedback:
                LogWrapper.Error(ex, desc);
                break;

            case LauncherLogLevel.Critical:
                LogWrapper.Fatal(ex, desc);
                break;

            case LauncherLogLevel.Debug:
                LogWrapper.Debug($"{desc}:{ex}");
                break;

            case LauncherLogLevel.Developer:
                LogWrapper.Trace($"{desc}:{ex}");
                break;

            case LauncherLogLevel.Normal:
            default:
                LogWrapper.Error(ex, desc);
                break;
        }
    }

    private static void ShowDebugHint(string message)
    {
        if (LauncherRuntime.ModeDebug)
            HintService.Hint("[调试模式] " + message, HintType.Info, false);
    }

    private static void ShowUserNotification(
        LauncherLogLevel level,
        string userMessage,
        string dialogTitle)
    {
        switch (level)
        {
            case LauncherLogLevel.Hint:
                HintService.Hint(userMessage, HintType.Error, false);
                break;

            case LauncherLogLevel.Msgbox:
                ModMain.MyMsgBox(userMessage, dialogTitle, isWarn: true);
                break;

            case LauncherLogLevel.Feedback:
                LauncherFeedbackService.ShowFeedbackPrompt(userMessage, dialogTitle, false);
                break;

            case LauncherLogLevel.Critical:
                LauncherFeedbackService.ShowFeedbackPrompt(userMessage, dialogTitle, true);
                break;

            case LauncherLogLevel.Normal
                or LauncherLogLevel.Developer
                or LauncherLogLevel.Debug:
            default:
                return;
        }
    }

    private static string GetUserDialogTitle(string? title)
    {
        return string.IsNullOrWhiteSpace(title)
            ? Lang.Text("SystemDialog.Error.Title")
            : title;
    }

    private static string GetUserExceptionMessage(string desc, Exception ex, string? userSummary)
    {
        if (!string.IsNullOrWhiteSpace(userSummary))
            return ExceptionDetails.Compose(userSummary, ex);

        return ex.GetType() == typeof(Win32Exception)
            ? Lang.Text("SystemDialog.Error.OperationFailed.RuntimeMessage", desc, ex.ToString())
            : Lang.Text("SystemDialog.Error.OperationFailed.Message", desc, ex.ToString());
    }
}