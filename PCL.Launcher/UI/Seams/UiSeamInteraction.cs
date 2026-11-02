using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;

namespace PCL.UI.Seams;

/// <summary>把业务层 seam 的通用交互落到界面的弹窗与提示服务。</summary>
// TODO: 本文件与 UiSeamPassive 中仍有大量 seam 是"最小实现/日志占位"：任务栏进度、下载按钮、
// 更新重启按钮、登录/皮肤页、Comp 详情与本地资源、整合包安装加载器、崩溃分析器、媒体控制、
// 页面导航等。它们不是等价实现，需在对应页面/控件迁移后替换为真实实现并删除 no-op 分支。
internal static class UiSeamInteraction
{
    internal static void Hint(string text, NotificationType type)
    {
        HintService.Show(
            type switch
            {
                NotificationType.Success => Hints.Success(text),
                NotificationType.Warning => Hints.Warning(text),
                NotificationType.Error => Hints.Error(text),
                _ => Hints.Info(text)
            });
    }

    internal static int Message(
        string text,
        string? title,
        string? button1,
        string? button2 = "",
        string? button3 = "",
        bool isWarn = false,
        Action? button1Action = null,
        Action? button2Action = null,
        Action? button3Action = null)
    {
        var buttons = new List<DialogButton>();
        Add(button1, button1Action);
        Add(button2, button2Action);
        Add(button3, button3Action);

        var request = new TextDialogRequest
        {
            Title = title ?? "",
            Content = text,
            Buttons = buttons.Count == 0 ? DialogButtons.Ok : DialogButtons.From(buttons.ToArray()),
            IsWarn = isWarn,
            ForceWait = true
        };
        return DialogService.Instance.Show(request).Result?.Button ?? 0;

        void Add(string? buttonText, Action? action)
        {
            if (!string.IsNullOrEmpty(buttonText))
                buttons.Add(DialogButton.Create(buttonText, action));
        }
    }

    internal static string? Input(string title, string content = "", string defaultValue = "")
    {
        return DialogService.Instance.Show(
                new InputDialogRequest
                {
                    Title = title,
                    Content = content,
                    DefaultValue = defaultValue,
                    ForceWait = true
                })
            .Result?.InputText;
    }

    internal static int? Select(string title, IReadOnlyList<string> options)
    {
        return DialogService.Instance.Show(
                new SelectDialogRequest
                {
                    Title = title,
                    Options =
                    [
                        .. options
                            .Select(option => new DialogOption
                            {
                                Text = option
                            })
                    ],
                    ForceWait = true
                })
            .Result?.SelectedIndex;
    }

    internal static List<int>? MultiSelect(string title, IReadOnlyList<string> options)
    {
        return DialogService
            .Instance.Show(
                new SelectDialogRequest
                {
                    Title = title,
                    Options = options
                        .Select(option => new DialogOption
                        {
                            Text = option
                        })
                        .ToArray(),
                    MultiSelect = true,
                    ForceWait = true
                })
            .Result?.SelectedIndices?.ToList();
    }

    internal static void OpenWebsite(string url)
    {
        try
        {
            Process.Start(
                new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "UI", $"打开网页失败：{url}");
        }
    }

    internal static void Shutdown()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            desktop)
            desktop.Shutdown();
    }

    /// <summary>
    ///     更新完成后重启：交给生命周期在服务停止（含单实例锁释放）后以相同参数重新拉起当前可执行文件。
    /// </summary>
    internal static void Restart()
    {
        try
        {
            Lifecycle.RequestRestartOnExit(_GetRestartArguments());
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "更新后重启请求失败，无法自动重启，请手动重新启动启动器");
            throw;
        }

        Shutdown();
    }

    private static string? _GetRestartArguments()
    {
        var arguments = Environment.GetCommandLineArgs();
        if (arguments.Length <= 1) return null;
        return string.Join(
            ' ',
            arguments
                .Skip(1)
                .Select(static argument =>
                    argument.Length == 0
                    || argument.Any(char.IsWhiteSpace)
                    || argument.Contains('"')
                        ? "\"" + argument.Replace("\"", "\\\"") + "\""
                        : argument));
    }

    internal static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
        ?.MainWindow;

    internal static void LogMissing(string feature)
    {
        LogWrapper.Debug("UI", $"seam {feature} 暂无界面实现，本次调用已忽略");
    }
}

/// <summary>业务层与用户的交互：提示、消息框、确认与打开网页。</summary>
public sealed class UserInteractionBridge : IUserInteraction
{
    public void ShowHint(string text, NotificationType type = NotificationType.Info)
    {
        UiSeamInteraction.Hint(text, type);
    }

    public void ShowMessageBox(
        string text,
        string? title = null,
        string? button1 = null,
        bool isWarn = false)
    {
        UiSeamInteraction.Message(text, title, button1, isWarn: isWarn);
    }

    public void OpenWebsite(string url)
    {
        UiSeamInteraction.OpenWebsite(url);
    }

    public bool ShowConfirm(
        string text,
        string? title = null,
        string? confirmText = null,
        string? cancelText = null)
    {
        return UiSeamInteraction.Message(
                text,
                title,
                confirmText ?? Lang.Text("Common.Action.Confirm"),
                cancelText ?? Lang.Text("Common.Action.Cancel"))
            == 1;
    }
}

/// <summary>更新流程的通知桥接；页面未迁移的部分仅记录日志。</summary>
public sealed class UpdateUiBridge : IUpdateUi
{
    public int ShowMessageBox(
        string caption,
        string? title = null,
        string? button1 = null,
        string? button2 = "",
        string? button3 = "",
        bool isWarn = false,
        Action? button1Action = null,
        Action? button2Action = null,
        Action? button3Action = null)
    {
        return UiSeamInteraction.Message(
            caption,
            title,
            button1,
            button2,
            button3,
            isWarn,
            button1Action,
            button2Action,
            button3Action);
    }

    public void ShowHint(string text, NotificationType type = NotificationType.Info)
    {
        UiSeamInteraction.Hint(text, type);
    }

    public void NavigateSetupUpdate()
    {
        UiSeamInteraction.LogMissing(nameof(NavigateSetupUpdate));
    }

    public void ShowUpdateRestartButton(string tooltip)
    {
        UiSeamInteraction.LogMissing(nameof(ShowUpdateRestartButton));
    }

    public void RefreshDownloadButton()
    {
        UiSeamInteraction.LogMissing(nameof(RefreshDownloadButton));
    }

    public void UpdateSetupButton(string text, bool enabled)
    {
        UiSeamInteraction.LogMissing(nameof(UpdateSetupButton));
    }

    public void ExitApplication(bool restart)
    {
        if (restart)
        {
            UiSeamInteraction.Restart();
            return;
        }

        UiSeamInteraction.Shutdown();
    }
}

/// <summary>公告按钮动作桥接：事件业务尚未迁移到界面层。</summary>
public sealed class AnnouncementActionsBridge : IAnnouncementActionHandler
{
    public void Execute(string command, string? parameter)
    {
        UiSeamInteraction.LogMissing($"AnnouncementAction({command})");
    }
}

/// <summary>启动流程通知桥接；启动页已有基础承载，其余交互记录日志。</summary>
public sealed class LaunchUiBridge : ILaunchUi
{
    public int ShowMessageBox(
        string text,
        string? title = null,
        string? button1 = null,
        string? button2 = "",
        string? button3 = "",
        bool isWarn = false,
        bool forceWait = false,
        Action? button1Action = null)
    {
        return UiSeamInteraction.Message(
            text,
            title,
            button1,
            button2,
            button3,
            isWarn,
            button1Action);
    }

    public void SetGpuPreference(string javaExePath, bool preference)
    {
        // 非 Windows 由启动进程的环境变量处理 GPU 偏好，无需修改可执行文件
    }

    public int? ShowSelect(string caption, IReadOnlyList<string> options)
    {
        return UiSeamInteraction.Select(caption, options);
    }

    public void OpenExplorer(string path)
    {
        Basics.OpenPath(path);
    }

    public void PrepareLaunchNavigation()
    {
        UiSeamInteraction.LogMissing(nameof(PrepareLaunchNavigation));
    }

    public void RefreshLaunchButtons()
    {
        UiSeamInteraction.LogMissing(nameof(RefreshLaunchButtons));
    }

    public void RefreshLaunchPage(bool show)
    {
        UiSeamInteraction.LogMissing(nameof(RefreshLaunchPage));
    }

    public void AppendLaunchLog(string text)
    {
        LogWrapper.Info("Launch", text);
    }

    public void ClearLaunchLog()
    {
        UiSeamInteraction.LogMissing(nameof(ClearLaunchLog));
    }

    public void ShowLoginPage()
    {
        UiSeamInteraction.LogMissing(nameof(ShowLoginPage));
    }

    public void ShowLaunchingPage()
    {
        UiSeamInteraction.LogMissing(nameof(ShowLaunchingPage));
    }

    public void RefreshLaunching()
    {
        UiSeamInteraction.LogMissing(nameof(RefreshLaunching));
    }

    public void AttachLogPage(GameWatcher.Watcher watcher)
    {
        UiSeamInteraction.LogMissing(nameof(AttachLogPage));
    }

    public void SetMainWindowState(LaunchWindowState state)
    {
        if (state == LaunchWindowState.Exit)
        {
            UiSeamInteraction.Shutdown();
            return;
        }

        var window = UiSeamInteraction.MainWindow;
        if (window is null) return;
        if (state == LaunchWindowState.Hide) window.Hide();
        else window.WindowState = WindowState.Minimized;
    }

    public (double Width, double Height) GetMainPanSize()
    {
        var window = UiSeamInteraction.MainWindow;
        return window is null ? (0, 0) : (window.Bounds.Width, window.Bounds.Height);
    }
}

/// <summary>Comp 业务的选择/输入弹窗桥接。</summary>
public sealed class CompUiBridge : ICompUi
{
    public int? ShowSelect(
        string caption,
        string? title,
        IReadOnlyList<string> options,
        string? confirmText = null,
        string? cancelText = null)
    {
        return UiSeamInteraction.Select(title ?? caption, options);
    }

    public List<int>? ShowMultiSelect(
        string caption,
        string? title,
        IReadOnlyList<string> options,
        string? confirmText = null,
        string? cancelText = null)
    {
        return UiSeamInteraction.MultiSelect(title ?? caption, options);
    }

    public string? ShowInput(string title, string text = "", string defaultInput = "")
    {
        return UiSeamInteraction.Input(title, text, defaultInput);
    }

    public void RefreshDownloadButton()
    {
        UiSeamInteraction.LogMissing(nameof(RefreshDownloadButton));
    }

    public void LoaderStateChangedHintOnly(Loading.LoaderBase loader)
    {
        UiSeamInteraction.LogMissing(nameof(LoaderStateChangedHintOnly));
    }

    public int ShowDialog(
        string caption,
        string? title,
        string? button1,
        string? button2 = "",
        string? button3 = "",
        bool isWarn = false)
    {
        return UiSeamInteraction.Message(caption, title, button1, button2, button3, isWarn);
    }
}

/// <summary>游戏监视器的通知桥接。</summary>
public sealed class WatcherUiBridge : IWatcherUi
{
    public void RefreshShutdownButton()
    {
        UiSeamInteraction.LogMissing(nameof(RefreshShutdownButton));
    }

    public void ExitApplication()
    {
        UiSeamInteraction.Shutdown();
    }

    public void ShowApplication()
    {
        var window = UiSeamInteraction.MainWindow;
        if (window is null) return;
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    public void ShowFeedback()
    {
        UiSeamInteraction.LogMissing(nameof(ShowFeedback));
    }

    public void ShowHint(string text, NotificationType type = NotificationType.Info)
    {
        UiSeamInteraction.Hint(text, type);
    }
}