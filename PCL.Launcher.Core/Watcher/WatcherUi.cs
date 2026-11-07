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

/// <summary>背景音乐的播放状态，与 WPF 的 <c>Music.MusicStates</c> 一致。</summary>
public enum MusicPlaybackState
{
    Stop,
    Play,
    Pause
}

/// <summary>游戏启动/结束时对媒体播放的控制，并提供设置页所需的背景音乐操作。</summary>
public interface IMediaService
{
    /// <summary>当前平台是否真正具备音乐播放能力。</summary>
    bool IsAvailable { get; }

    /// <summary>当前平台是否具备背景音乐播放能力。</summary>
    bool IsMusicAvailable => IsAvailable;

    /// <summary>当前音乐播放状态。</summary>
    MusicPlaybackState MusicState => MusicPlaybackState.Stop;

    /// <summary>播放列表中的音乐数量。</summary>
    int MusicCount => 0;

    /// <summary>当前正在播放的音乐文件路径；尚未播放时为 <see langword="null" />。</summary>
    string? CurrentMusicPath => null;

    /// <summary>当前播放列表快照。</summary>
    IReadOnlyList<string> MusicList => [];

    void PauseMusic();
    void ResumeMusic();

    /// <summary>左键点击音乐快捷按钮：暂停、恢复或重新开始。</summary>
    void ToggleMusic()
    {
        switch (MusicState)
        {
            case MusicPlaybackState.Pause:
                ResumeMusic();
                break;

            case MusicPlaybackState.Play:
                PauseMusic();
                break
                    ;
            case MusicPlaybackState.Stop:
            default:
                RefreshMusic(false);
                break;
        }
    }

    /// <summary>右键点击音乐快捷按钮：播放下一首；只有一首时从头重播。</summary>
    void PlayNextMusic()
    {
    }

    /// <summary>刷新播放列表并开始播放，对应 WPF 的 <c>MusicRefreshPlay</c>。</summary>
    void RefreshMusic(bool showHint, bool isFirstLoad = false)
    {
    }

    /// <summary>停止播放并清空内存中的播放列表；不会删除磁盘文件。</summary>
    void ClearMusic()
    {
    }

    /// <summary>应用音乐音量，取值范围与配置一致（0-1000）。</summary>
    void SetMusicVolume(int volume)
    {
    }
}

public static class MediaService
{
    public static IMediaService? Current { get; set; }
}