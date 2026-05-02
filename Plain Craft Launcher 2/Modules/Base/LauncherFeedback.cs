using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;

namespace PCL;

public enum HintKind
{
    Info,
    Finish,
    Critical
}

/// <summary>
/// Presents launcher feedback, hints, and message callbacks through an optional UI sink.
/// </summary>
public static class LauncherFeedback
{
    private static volatile bool _isInFeedback;

    public static ILauncherFeedbackSink? Sink { get; set; }

    public static void ShowHint(string text, HintKind kind = HintKind.Info)
    {
        Execute(sink => sink.ShowHint(text, kind), "show hint");
    }

    public static int ShowMessage(string text, string title, string button1 = "确定", string button2 = "",
        bool isWarning = false)
    {
        return Execute(sink => sink.ShowMessage(text, title, button1, button2, isWarning), 0, "show message");
    }

    public static bool CanFeedback(bool showHint)
    {
        var stat = ModSecret.GetVersionStatus();
        if (stat != ModSecret.VersionStatus.Latest)
        {
            if (showHint)
                Execute(sink => sink.CanFeedback(true), false, "show feedback unavailable message");

            return false;
        }

        return true;
    }

    public static void NavigateToUpdatePage()
    {
        Execute(sink => sink.NavigateToUpdatePage(), "navigate to update page");
    }

    public static void Feedback(bool showMsgbox = true, bool forceOpenLog = false)
    {
        FeedbackInfo();
        var currentDate = Strings.Format(DateTime.Now, "yyyy-M-dd");

        if (forceOpenLog || (showMsgbox &&
                             ShowMessage(
                                 "若你在汇报一个 Bug，请点击 打开文件夹 按钮，并上传 Launch-" + currentDate + "-[一串数字].log 中包含错误信息的文件。" +
                                 "\r\n游戏崩溃一般与启动器无关，请不要因为游戏崩溃而提交反馈。", "反馈提交提醒", "打开文件夹", "不需要") == 1))
            LauncherShell.OpenExplorer(LauncherPaths.ExecutableDirectory + @"PCL\Log\");

        LauncherShell.OpenWebsite("https://github.com/PCL-Community/PCL2-CE/issues/");
    }

    /// <summary>
    /// Writes diagnostic environment information into the launcher log.
    /// </summary>
    public static void FeedbackInfo()
    {
        try
        {
            var physicalMemory = KernelInterop.GetPhysicalMemoryBytes();
            var availableMb = physicalMemory.Available / 1024 / 1024;
            var totalMb = physicalMemory.Total / 1024 / 1024;
            var dpi = (int)Math.Round(Graphics.FromHwnd(nint.Zero).DpiX);
            var dpiScale = Math.Round(dpi / 96.0, 2);

            var info = $"[System] Diagnostic Information:{"\r\n"}" +
                       $"OS: {RuntimeInformation.OSDescription} (32-bit: {LauncherEnvironment.Is32BitSystem}){"\r\n"}" +
                       $"Memory: {availableMb} MB / {totalMb} MB{"\r\n"}" +
                       $"DPI: {dpi} ({dpiScale * 100}%){"\r\n"}" +
                       $"MC Folder: {ModMinecraft.McFolderSelected ?? "Nothing"}{"\r\n"}" +
                       $"Executable Path: {LauncherPaths.ExecutableDirectory}";

            LogWrapper.Info(info);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Failed to collect feedback information");
        }
    }

    private static void Execute(Action<ILauncherFeedbackSink> action, string operation)
    {
        if (Sink is null)
            return;
        if (_isInFeedback)
        {
            LogWrapper.Warn($"[LauncherFeedback] Skipped nested feedback request: {operation}");
            return;
        }

        try
        {
            _isInFeedback = true;
            action(Sink);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, $"[LauncherFeedback] Failed to {operation}");
        }
        finally
        {
            _isInFeedback = false;
        }
    }

    private static T Execute<T>(Func<ILauncherFeedbackSink, T> action, T fallback, string operation)
    {
        if (Sink is null)
            return fallback;
        if (_isInFeedback)
        {
            LogWrapper.Warn($"[LauncherFeedback] Skipped nested feedback request: {operation}");
            return fallback;
        }

        try
        {
            _isInFeedback = true;
            return action(Sink);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, $"[LauncherFeedback] Failed to {operation}");
            return fallback;
        }
        finally
        {
            _isInFeedback = false;
        }
    }
}

/// <summary>
/// Provides UI feedback presentation for launcher infrastructure without referencing ModMain.
/// </summary>
public interface ILauncherFeedbackSink
{
    void ShowHint(string text, HintKind kind);

    int ShowMessage(string text, string title, string button1, string button2, bool isWarning);

    bool CanFeedback(bool showHint);

    void NavigateToUpdatePage();
}
