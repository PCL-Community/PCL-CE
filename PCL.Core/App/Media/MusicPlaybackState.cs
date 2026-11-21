namespace PCL.Core.App.Media;

/// <summary>背景音乐的播放状态。</summary>
public enum MusicPlaybackState
{
    /// <summary>当前没有正在播放的音乐。</summary>
    Stop,

    /// <summary>音乐正在播放。</summary>
    Play,

    /// <summary>音乐已暂停，可以继续播放。</summary>
    Pause
}