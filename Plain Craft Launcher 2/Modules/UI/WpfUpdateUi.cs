using PCL.Core.Platform;

namespace PCL;

/// <summary>将更新业务通知桥接到 WPF 主窗口。</summary>
public sealed class WpfUpdateUi : IUpdateUi
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
        return DialogCompat.ShowMessageBox(
            caption,
            title,
            button1,
            button2,
            button3,
            isWarn,
            button1Action: button1Action,
            button2Action: button2Action,
            button3Action: button3Action);
    }

    public void ShowHint(string text, NotificationType type = NotificationType.Info)
    {
        var hintType = type switch
        {
            NotificationType.Success => NotificationType.Success,
            NotificationType.Warning => NotificationType.Warning,
            NotificationType.Error => NotificationType.Error,
            _ => NotificationType.Info
        };
        DialogCompat.ShowHint(text, hintType);
    }

    public void NavigateSetupUpdate()
    {
        Launcher.frmMain?.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupUpdate);
    }

    public void ShowUpdateRestartButton(string tooltip)
    {
        PlatformServices.UiDispatcher?.Post(() =>
        {
            if (Launcher.frmMain is not { } main) return;
            LocalizedText.TryBind(main.BtnExtraUpdateRestart, "ToolTip", () => tooltip);
            main.BtnExtraUpdateRestart.ShowRefresh();
            main.BtnExtraUpdateRestart.Ribble();
        });
    }

    public void RefreshDownloadButton()
    {
        PlatformServices.UiDispatcher?.Post(() =>
        {
            Launcher.frmMain?.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain?.BtnExtraDownload.Ribble();
        });
    }

    public void UpdateSetupButton(string text, bool enabled)
    {
        PlatformServices.UiDispatcher?.Post(() =>
        {
            if (Launcher.frmSetupUpdate is not { } page) return;
            page.BtnUpdate.TrySetLocalizedValue("Text", text);
            page.BtnUpdate.IsEnabled = enabled;
        });
    }

    public void ExitApplication(bool restart)
    {
        PlatformServices.UiDispatcher?.Post(() =>
            Launcher.frmMain?.EndProgram(false, restart));
    }
}

/// <summary>公告按钮事件桥接。</summary>
public sealed class WpfAnnouncementActions : IAnnouncementActionHandler
{
    public void Execute(string command, string? parameter)
    {
        if (EventTypeMapper.TryParse(command, out var eventType))
            CustomEvent.Raise(eventType, parameter);
    }
}