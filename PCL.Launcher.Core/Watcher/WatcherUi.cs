namespace PCL;

/// <summary>游戏日志级别。</summary>
public enum GameLogLevel
{
    Debug,
    Info,
    Warn,
    Error,
    Fatal
}

/// <summary>游戏监视器向 UI 层发出的通知。</summary>
public interface IWatcherUi
{
    void RefreshShutdownButton();
    void ExitApplication();
    void ShowApplication();
    void ShowFeedback();
    void ShowHint(string text, NotificationType type = NotificationType.Info);
}

public static class WatcherUi
{
    public static IWatcherUi? Current { get; set; }
}

/// <summary>游戏启动/结束时对媒体播放的控制。</summary>
public interface IMediaService
{
    /// <summary>当前平台是否真正具备音乐/视频播放能力。</summary>
    bool IsAvailable { get; }

    void PauseMusic();
    void ResumeMusic();
    void SetVideoGaming(bool gaming);
}

public static class MediaService
{
    public static IMediaService? Current { get; set; }
}