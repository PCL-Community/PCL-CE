using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Win32;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.Views.Shell;

namespace PCL.UI.Seams;

/// <summary>把业务层 seam 的通用交互落到界面的弹窗、提示与导航服务。</summary>
internal static class UiSeamInteraction
{
    internal static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
        ?.MainWindow;

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

    /// <summary>把动作调度到 UI 线程；界面尚未就绪时直接执行，由动作自身处理空窗口。</summary>
    internal static void RunOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }

    internal static void WarnMissing(string feature, string todoId)
    {
        LogWrapper.Warn(
            "Seam",
            $"seam {feature} 的界面宿主尚未接入（{todoId}），本次调用已跳过；需要页面/壳登记对应 SeamContracts 接口");
    }

    internal static void NotImplemented(string feature, string todoId)
    {
        LogWrapper.Warn(
            "Seam",
            $"seam {feature} 尚未实现（TODO({todoId})），本次调用已忽略");
    }

    internal static bool TryShellAction(
        string feature,
        string todoId,
        Action<ISeamShellActions> action)
    {
        var shell = SeamHosts.Shell;
        if (shell is not null)
        {
            RunOnUi(() => action(shell));
            return true;
        }

        WarnMissing(feature, todoId);
        return false;
    }

    internal static void RefreshDownloadButton(string feature)
    {
        if (SeamHosts.Shell is { } shell)
        {
            RunOnUi(shell.RefreshDownloadButton);
            return;
        }

        var panel = ExtraButtonsPanel.Current;
        if (panel is not null)
        {
            RunOnUi(panel.RefreshDownloadButton);
            return;
        }

        WarnMissing(feature, "SEAM-SHELL");
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

/// <summary>更新流程的通知桥接：更新页导航、重启按钮、下载按钮与设置页按钮都接到真实界面。</summary>
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
        PageRouter.Navigate(PageType.Setup, PageSubType.SetupUpdate);
    }

    public void ShowUpdateRestartButton(string tooltip)
    {
        UiSeamInteraction.TryShellAction(
            nameof(ShowUpdateRestartButton),
            "SEAM-SHELL",
            shell => shell.ShowUpdateRestartButton(tooltip));
    }

    public void RefreshDownloadButton()
    {
        UiSeamInteraction.RefreshDownloadButton(nameof(RefreshDownloadButton));
    }

    public void UpdateSetupButton(string text, bool enabled)
    {
        var page = SeamHosts.SetupUpdatePage
            ?? PageRouter.CurrentPage as ISeamSetupUpdatePage;
        if (page is null)
        {
            UiSeamInteraction.WarnMissing(nameof(UpdateSetupButton), "SEAM-SETUP");
            return;
        }

        UiSeamInteraction.RunOnUi(() => page.SetUpdateButton(text, enabled));
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

/// <summary>公告按钮动作桥接：把公告里的命令交给自定义事件分发。</summary>
public sealed class AnnouncementActionsBridge : IAnnouncementActionHandler
{
    public void Execute(string command, string? parameter)
    {
        if (!EventTypeMapper.TryParse(command, out var eventType))
        {
            LogWrapper.Warn("Seam", $"公告动作命令无法识别：{command}");
            return;
        }

        if (EventTypeMapper.IsUnsupportedType(command))
        {
            LogWrapper.Warn("Seam", $"公告动作命令在当前版本不支持：{command}");
            return;
        }

        CustomEvent.Raise(eventType, parameter ?? "");
    }
}

/// <summary>启动流程通知桥接：导航、日志与页面状态都转发到启动页/日志页宿主。</summary>
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
        if (!OperatingSystem.IsWindows())
        {
            LogWrapper.Debug(
                "Seam",
                $"当前平台不支持写入 GPU 偏好（{javaExePath}），已跳过");
            return;
        }

        try
        {
            const string registryKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
            const string highPerformance = "GpuPreference=2;";
            const string defaultPreference = "GpuPreference=0;";
            var currentIsHighPerformance = false;
            using (var readOnlyKey = Registry.CurrentUser.OpenSubKey(
                       registryKey,
                       false))
            {
                if (readOnlyKey is null)
                {
                    Registry.CurrentUser.CreateSubKey(registryKey);
                }
                else
                {
                    var currentValue = readOnlyKey.GetValue(javaExePath)?.ToString();
                    currentIsHighPerformance = currentValue == highPerformance;
                }
            }

            if (currentIsHighPerformance == preference) return;
            using var writeKey = Registry.CurrentUser.OpenSubKey(
                registryKey,
                true);
            writeKey?.SetValue(
                javaExePath,
                preference ? highPerformance : defaultPreference);
            LogWrapper.Info(
                "Seam",
                $"已调整程序 {javaExePath} 的显卡设置为高性能：{preference}");
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Seam", $"写入 GPU 偏好失败：{javaExePath}");
        }
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
        PageRouter.Navigate(PageType.Launch);
    }

    public void RefreshLaunchButtons()
    {
        if (SeamHosts.LaunchPage is { } page)
        {
            UiSeamInteraction.RunOnUi(page.RefreshLaunchButtons);
            return;
        }

        UiSeamInteraction.WarnMissing(nameof(RefreshLaunchButtons), "SEAM-LAUNCH");
    }

    public void RefreshLaunchPage(bool show)
    {
        if (SeamHosts.LaunchPage is { } page)
        {
            UiSeamInteraction.RunOnUi(() => page.RefreshLaunchPage(show));
            return;
        }

        UiSeamInteraction.WarnMissing(nameof(RefreshLaunchPage), "SEAM-LAUNCH");
    }

    public void AppendLaunchLog(string text)
    {
        LogWrapper.Info("Launch", text);
        if (SeamHosts.LaunchLog is { } sink)
        {
            UiSeamInteraction.RunOnUi(() => sink.AppendLaunchLog(text));
            return;
        }

        UiSeamInteraction.WarnMissing(nameof(AppendLaunchLog), "SEAM-LAUNCH");
    }

    public void ClearLaunchLog()
    {
        if (SeamHosts.LaunchLog is { } sink)
        {
            UiSeamInteraction.RunOnUi(sink.ClearLaunchLog);
            return;
        }

        UiSeamInteraction.WarnMissing(nameof(ClearLaunchLog), "SEAM-LAUNCH");
    }

    public void ShowLoginPage()
    {
        if (SeamHosts.LaunchPage is { } page)
        {
            UiSeamInteraction.RunOnUi(page.ShowLogin);
            return;
        }

        UiSeamInteraction.WarnMissing(nameof(ShowLoginPage), "SEAM-LAUNCH");
    }

    public void ShowLaunchingPage()
    {
        if (SeamHosts.LaunchPage is { } page)
        {
            UiSeamInteraction.RunOnUi(page.ShowLaunching);
            return;
        }

        UiSeamInteraction.WarnMissing(nameof(ShowLaunchingPage), "SEAM-LAUNCH");
    }

    public void RefreshLaunching()
    {
        if (SeamHosts.LaunchPage is { } page)
        {
            UiSeamInteraction.RunOnUi(page.RefreshLaunching);
            return;
        }

        UiSeamInteraction.WarnMissing(nameof(RefreshLaunching), "SEAM-LAUNCH");
    }

    public void AttachLogPage(GameWatcher.Watcher watcher)
    {
        // 导航与登记必须同处 UI 线程：宿主在导航过程中实例化日志页并登记 SeamHosts.GameLog
        UiSeamInteraction.RunOnUi(() =>
        {
            PageRouter.Navigate(PageType.GameLog);
            if (SeamHosts.GameLog is { } sink)
            {
                sink.AttachWatcher(watcher);
                return;
            }

            UiSeamInteraction.WarnMissing(nameof(AttachLogPage), "SEAM-LOG");
        });
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

/// <summary>Comp 业务的选择/输入弹窗与下载按钮桥接。</summary>
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
        UiSeamInteraction.RefreshDownloadButton(nameof(RefreshDownloadButton));
    }

    public void LoaderStateChangedHintOnly(Loading.LoaderBase loader)
    {
        switch (loader.State)
        {
            case LoadState.Finished:
                UiSeamInteraction.Hint(
                    $"{loader.DisplayName}{Lang.Text("Common.Status.Success")}",
                    NotificationType.Success);
                break;
            case LoadState.Failed:
                UiSeamInteraction.Hint(
                    $"{loader.DisplayName}{Lang.Text("Common.Status.Failure")}{loader.Error?.Message}",
                    NotificationType.Error);
                break;
            case LoadState.Aborted:
                UiSeamInteraction.Hint(
                    $"{loader.DisplayName}{Lang.Text("Common.Status.Cancelled")}",
                    NotificationType.Info);
                break;
            case LoadState.Waiting:
            case LoadState.Loading:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(loader));
        }
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

/// <summary>游戏监视器与关闭按钮的通知桥接。</summary>
public sealed class WatcherUiBridge : IWatcherUi
{
    public void RefreshShutdownButton()
    {
        UiSeamInteraction.TryShellAction(
            nameof(RefreshShutdownButton),
            "SEAM-SHELL",
            shell => shell.RefreshShutdownButton());
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
        PageRouter.Navigate(PageType.Setup, PageSubType.SetupFeedback);
    }

    public void ShowHint(string text, NotificationType type = NotificationType.Info)
    {
        UiSeamInteraction.Hint(text, type);
    }
}