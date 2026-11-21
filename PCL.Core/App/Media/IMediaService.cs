using System.Collections.Generic;

namespace PCL.Core.App.Media;

/// <summary>启动器背景音乐服务。具体播放实现由界面程序集注册到 <see cref="MediaService" />。</summary>
public interface IMediaService
{
    /// <summary>当前平台是否具备音频播放能力。</summary>
    bool IsAvailable { get; }

    /// <summary>当前平台是否具备背景音乐播放能力。</summary>
    bool IsMusicAvailable => IsAvailable;

    /// <summary>当前音乐播放状态。</summary>
    MusicPlaybackState MusicState => MusicPlaybackState.Stop;

    /// <summary>播放列表中的音乐数量。</summary>
    int MusicCount => 0;

    /// <summary>当前正在播放的音乐文件路径；尚未开始时为 <see langword="null" />。</summary>
    string? CurrentMusicPath => null;

    /// <summary>当前播放列表快照。</summary>
    IReadOnlyList<string> MusicList => [];

    /// <summary>暂停背景音乐。</summary>
    void PauseMusic();

    /// <summary>恢复背景音乐；没有当前音乐时由实现决定是否重新开始播放。</summary>
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
                break;

            default:
                RefreshMusic(false);
                break;
        }
    }

    /// <summary>右键点击音乐快捷按钮：播放下一首；只有一首时从头重播。</summary>
    void PlayNextMusic()
    {
    }

    /// <summary>刷新播放列表并开始播放。</summary>
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