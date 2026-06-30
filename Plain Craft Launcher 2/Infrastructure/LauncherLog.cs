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
        ModBase.LogLevel level = ModBase.LogLevel.Normal,
        string? title = null,
        string? userSummary = null)
    {
        WriteTextLog(text, level);

        if (LauncherRuntime.IsProgramEnded|| level is ModBase.LogLevel.Normal or ModBase.LogLevel.Developer)
            return;

        if (level == ModBase.LogLevel.Debug)
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
        ModBase.LogLevel level = ModBase.LogLevel.Debug,
        string? title = null,
        string? userSummary = null)
    {
        if (ex is ThreadInterruptedException)
            return;

        WriteExceptionLog(ex, desc, level);

        if (LauncherRuntime.IsProgramEnded || level is ModBase.LogLevel.Normal or ModBase.LogLevel.Developer)
            return;

        if (level == ModBase.LogLevel.Debug)
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

    private static void WriteTextLog(string text, ModBase.LogLevel level)
    {
        switch (level)
        {
            case ModBase.LogLevel.Msgbox or ModBase.LogLevel.Hint:
                LogWrapper.Warn(text);
                break;

            case ModBase.LogLevel.Feedback:
                LogWrapper.Error(text);
                break;

            case ModBase.LogLevel.Critical:
                LogWrapper.Fatal(text);
                break;

            case ModBase.LogLevel.Debug:
                LogWrapper.Debug(text);
                break;

            case ModBase.LogLevel.Developer:
                LogWrapper.Trace(text);
                break;

            case ModBase.LogLevel.Normal:
            default:
                LogWrapper.Info(text);
                break;
        }
    }

    private static void WriteExceptionLog(Exception ex, string desc, ModBase.LogLevel level)
    {
        switch (level)
        {
            case ModBase.LogLevel.Msgbox or ModBase.LogLevel.Hint:
                LogWrapper.Warn(ex, desc);
                break;

            case ModBase.LogLevel.Feedback:
                LogWrapper.Error(ex, desc);
                break;

            case ModBase.LogLevel.Critical:
                LogWrapper.Fatal(ex, desc);
                break;

            case ModBase.LogLevel.Debug:
                LogWrapper.Debug($"{desc}:{ex}");
                break;

            case ModBase.LogLevel.Developer:
                LogWrapper.Trace($"{desc}:{ex}");
                break;

            case ModBase.LogLevel.Normal:
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
        ModBase.LogLevel level,
        string userMessage,
        string dialogTitle)
    {
        switch (level)
        {
            case ModBase.LogLevel.Hint:
                HintService.Hint(userMessage, HintType.Error, false);
                break;

            case ModBase.LogLevel.Msgbox:
                ModMain.MyMsgBox(userMessage, dialogTitle, isWarn: true);
                break;

            case ModBase.LogLevel.Feedback:
                LauncherFeedbackService.ShowFeedbackPrompt(userMessage, dialogTitle, false);
                break;

            case ModBase.LogLevel.Critical:
                LauncherFeedbackService.ShowFeedbackPrompt(userMessage, dialogTitle, true);
                break;

            case ModBase.LogLevel.Normal
                or ModBase.LogLevel.Developer
                or ModBase.LogLevel.Debug:
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