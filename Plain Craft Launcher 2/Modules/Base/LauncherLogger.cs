using System;
using System.ComponentModel;
using System.Threading;
using Microsoft.VisualBasic;
using PCL.Core.Logging;

namespace PCL;

/// <summary>
/// Owns launcher logging levels, log writing, and debug assertions without direct UI dependencies.
/// </summary>
public static class LauncherLogger
{
    public static bool ModeDebug { get; set; }

    public enum LogLevel
    {
        Normal = 0,
        Developer = 1,
        Debug = 2,
        Hint = 3,
        Msgbox = 4,
        Feedback = 5,
        Critical = 6
    }

    private static bool _isCriticalErrorTriggered;

    /// <summary>
    /// Writes a textual log entry and raises user-visible feedback when requested.
    /// </summary>
    public static void Log(string text, LogLevel level = LogLevel.Normal, string title = "出现错误")
    {
        if (level is LogLevel.Msgbox or LogLevel.Hint)
            LogWrapper.Warn(text);
        else if (level == LogLevel.Feedback)
            LogWrapper.Error(text);
        else if (level == LogLevel.Critical)
            LogWrapper.Fatal(text);
        else if (level == LogLevel.Debug)
            LogWrapper.Debug(text);
        else if (level == LogLevel.Developer)
            LogWrapper.Trace(text);
        else
            LogWrapper.Info(text);

        if (LauncherEnvironment.IsProgramEnded || level == LogLevel.Normal)
            return;

        text = text.RegexReplace(@"\[[^\]]+?\] ", "");

        switch (level)
        {
            case LogLevel.Developer:
                return;
            case LogLevel.Debug:
                if (ModeDebug)
                    LauncherFeedback.ShowHint("[调试模式] " + text, HintKind.Info);
                return;
            case LogLevel.Hint:
                LauncherFeedback.ShowHint(text, HintKind.Critical);
                return;
            case LogLevel.Msgbox:
                LauncherFeedback.ShowMessage(text, title, "确定", "", true);
                return;
            case LogLevel.Feedback:
                if (LauncherFeedback.CanFeedback(false))
                {
                    if (LauncherFeedback.ShowMessage(text + "\r\n\r\n是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！",
                            title, "反馈", "取消", true) == 1)
                        LauncherFeedback.Feedback(false, true);
                }
                else
                {
                    LauncherFeedback.ShowMessage(text + "\r\n\r\n将 PCL 更新至最新版或许可以解决这个问题……", title,
                        "确定", "", true);
                }

                return;
            case LogLevel.Critical:
                if (_isCriticalErrorTriggered)
                {
                    FormMain.EndProgramForce(ProcessReturnValues.Exception);
                    return;
                }

                _isCriticalErrorTriggered = true;
                if (LauncherFeedback.CanFeedback(false))
                {
                    if (Interaction.MsgBox(text + "\r\n\r\n是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！",
                            (MsgBoxStyle)((int)MsgBoxStyle.Critical + (int)MsgBoxStyle.YesNo), title) == MsgBoxResult.Yes)
                        LauncherFeedback.Feedback(false, true);
                }
                else
                {
                    Interaction.MsgBox(text + "\r\n\r\n将 PCL 更新至最新版或许可以解决这个问题……", MsgBoxStyle.Critical,
                        title);
                }

                return;
        }
    }

    /// <summary>
    /// Writes an exception log entry and raises user-visible feedback when requested.
    /// </summary>
    public static void Log(Exception ex, string desc, LogLevel level = LogLevel.Debug, string title = "出现错误")
    {
        if (ex is ThreadInterruptedException)
            return;

        var fullMessage = desc + "：" + ex.Message;

        if (level is LogLevel.Msgbox or LogLevel.Hint)
            LogWrapper.Warn(ex, desc);
        else if (level == LogLevel.Feedback)
            LogWrapper.Error(ex, desc);
        else if (level == LogLevel.Critical)
            LogWrapper.Fatal(ex, desc);
        else if (level == LogLevel.Debug)
            LogWrapper.Debug($"{desc}:{ex}");
        else if (level == LogLevel.Developer)
            LogWrapper.Trace($"{desc}:{ex}");
        else
            LogWrapper.Error(ex, desc);

        if (LauncherEnvironment.IsProgramEnded)
            return;

        if (ex.GetType() == typeof(Win32Exception))
            fullMessage += "\r\n与系统底层交互失败，请尝试重新安装 .NET 8 解决此问题";

        switch (level)
        {
            case LogLevel.Normal:
            case LogLevel.Developer:
                return;
            case LogLevel.Debug:
            {
                if (ModeDebug)
                    LauncherFeedback.ShowHint("[调试模式] " + desc + "：" + ex, HintKind.Info);
                return;
            }
            case LogLevel.Hint:
                LauncherFeedback.ShowHint(desc + "：" + ex, HintKind.Critical);
                return;
            case LogLevel.Msgbox:
                LauncherFeedback.ShowMessage(fullMessage, title, "确定", "", true);
                return;
            case LogLevel.Feedback:
                if (LauncherFeedback.CanFeedback(false))
                {
                    if (LauncherFeedback.ShowMessage(fullMessage + "\r\n\r\n是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！",
                            title, "反馈", "取消", true) == 1)
                        LauncherFeedback.Feedback(false, true);
                }
                else
                {
                    LauncherFeedback.ShowMessage(fullMessage + "\r\n\r\n将 PCL 更新至最新版或许可以解决这个问题……", title,
                        "确定", "", true);
                }

                return;
            case LogLevel.Critical:
                if (_isCriticalErrorTriggered)
                {
                    FormMain.EndProgramForce(ProcessReturnValues.Exception);
                    return;
                }

                _isCriticalErrorTriggered = true;
                if (LauncherFeedback.CanFeedback(false))
                {
                    if (Interaction.MsgBox(fullMessage + "\r\n\r\n是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！",
                            (MsgBoxStyle)((int)MsgBoxStyle.Critical + (int)MsgBoxStyle.YesNo), title) == MsgBoxResult.Yes)
                        LauncherFeedback.Feedback(false, true);
                }
                else
                {
                    Interaction.MsgBox(fullMessage + "\r\n\r\n将 PCL 更新至最新版或许可以解决这个问题……", MsgBoxStyle.Critical,
                        title);
                }

                return;
        }
    }

    public static void DebugAssert(bool expression)
    {
        if (!expression)
            throw new Exception("断言命中");
    }
}
