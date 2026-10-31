using PCL.Core.Platform;

namespace PCL;

/// <summary>将游戏监视器通知桥接到 WPF 主窗口。</summary>
public sealed class WpfWatcherUi : IWatcherUi
{
    public void RefreshShutdownButton()
    {
        PlatformServices.UiDispatcher?.Post(() =>
            Launcher.frmMain?.BtnExtraShutdown.ShowRefresh());
    }

    public void ExitApplication()
    {
        PlatformServices.UiDispatcher?.Post(() =>
            Launcher.frmMain?.EndProgram(false));
    }

    public void ShowApplication()
    {
        PlatformServices.UiDispatcher?.Post(() =>
        {
            if (Launcher.frmMain is { } main) main.Hidden = false;
        });
    }

    public void ShowFeedback()
    {
        Common.FeedbackInfo();
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
}

/// <summary>媒体服务桥接。</summary>
public sealed class WpfMediaService : IMediaService
{
    public void PauseMusic()
    {
        Music.MusicPause();
    }

    public void ResumeMusic()
    {
        Music.MusicResume();
    }

    public void SetVideoGaming(bool gaming)
    {
        VideoBackground.IsGaming = gaming;
        if (gaming) VideoBackground.VideoPause();
        else VideoBackground.VideoPlay();
    }
}