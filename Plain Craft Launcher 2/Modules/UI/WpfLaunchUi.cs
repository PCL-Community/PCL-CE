using System.Windows;
using PCL.Core.Platform;

namespace PCL;

public sealed class WpfLaunchUi : ILaunchUi
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
        return DialogCompat.ShowMessageBox(
            text,
            title,
            button1,
            button2,
            button3,
            isWarn,
            forceWait: forceWait,
            button1Action: button1Action);
    }

    public void SetGpuPreference(string javaExePath, bool preference)
    {
        Launcher.SetGpuPreference(javaExePath, preference);
    }

    public int? ShowSelect(string caption, IReadOnlyList<string> options)
    {
        return DialogCompat.ShowSelect(caption, null, options);
    }

    public void OpenExplorer(string path)
    {
        Common.OpenExplorer(path);
    }

    public void PrepareLaunchNavigation()
    {
        PlatformServices.UiDispatcher?.Post(() =>
        {
            if (Launcher.frmMain is not { } main) return;
            main.AprilGiveup();
            main.pageStack =
                [.. main.pageStack.Where(p => p.page != FormMain.PageType.InstanceSelect)];
        });
    }

    public void RefreshLaunchButtons()
    {
        PlatformServices.UiDispatcher?.Post(() => Launcher.frmLaunchLeft?.RefreshButtonsUI());
    }

    public void RefreshLaunchPage(bool show)
    {
        PlatformServices.UiDispatcher?.Post(() => Launcher.frmLaunchLeft?.RefreshPage(show));
    }

    public void AppendLaunchLog(string text)
    {
        PlatformServices.UiDispatcher?.Post(() =>
        {
            if (Launcher.frmLaunchRight is { } right) right.LabLog.Text += text;
        });
    }

    public void ClearLaunchLog()
    {
        PlatformServices.UiDispatcher?.Post(() =>
        {
            if (Launcher.frmLaunchRight is { } right) right.LabLog.Text = "";
        });
    }

    public void ShowLoginPage()
    {
        PlatformServices.UiDispatcher?.Post(() => Launcher.frmLaunchLeft?.PageChangeToLogin());
    }

    public void ShowLaunchingPage()
    {
        PlatformServices.UiDispatcher?.Post(() => Launcher.frmLaunchLeft?.PageChangeToLaunching());
    }

    public void RefreshLaunching()
    {
        PlatformServices.UiDispatcher?.Post(() => Launcher.frmLaunchLeft?.LaunchingRefresh());
    }

    public void AttachLogPage(GameWatcher.Watcher watcher)
    {
        PlatformServices.UiDispatcher?.Post(() =>
        {
            Launcher.frmLogLeft ??= new PageLogLeft();
            if (Launcher.frmLogRight is null)
            {
                Animations.AniBeginControl();
                Launcher.frmLogRight = new PageLogRight();
                Animations.AniEndControl();
            }

            Launcher.frmLogLeft.Add(watcher);
        });
    }

    public void SetMainWindowState(LaunchWindowState state)
    {
        PlatformServices.UiDispatcher?.Post(() =>
        {
            if (Launcher.frmMain is not { } main) return;
            switch (state)
            {
                case LaunchWindowState.Exit:
                    main.EndProgram(false); break;
                case LaunchWindowState.Hide:
                    main.Hidden = true; break;
                case LaunchWindowState.Minimize:
                    main.WindowState = WindowState.Minimized; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        });
    }

    public (double Width, double Height) GetMainPanSize()
    {
        return PlatformServices.UiDispatcher?.Invoke(() =>
        {
            return Launcher.frmMain is { } main
                ? (main.PanForm.ActualWidth, main.PanForm.ActualHeight)
                : (0d, 0d);
        }) ?? (0d, 0d);
    }
}