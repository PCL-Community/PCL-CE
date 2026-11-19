using PCL.Core.App.Localization;
using PCL.Media;
using PCL.Views.Shell;

namespace PCL.UI.Bridges;

/// <summary>游戏启动前后的音乐控制，以及设置页与右下角音乐按钮所需的媒体操作。</summary>
public sealed class MediaServiceBridge : IMediaService, IDisposable
{
    private readonly MediaPlaybackService _media = new();

    public MediaServiceBridge()
    {
        ExtraButtonsPanel.MusicStateProvider = GetMusicButtonState;
        ExtraButtonsPanel.MusicToggleAction = ToggleMusic;
        ExtraButtonsPanel.MusicNextAction = PlayNextMusic;
    }

    public void Dispose()
    {
        _media.Dispose();
    }

    public bool IsAvailable => _media.IsAvailable;

    public bool IsMusicAvailable => _media.IsMusicAvailable;

    public MusicPlaybackState MusicState => _media.MusicState;

    public int MusicCount => _media.MusicCount;

    public string? CurrentMusicPath => _media.CurrentMusicPath;

    public IReadOnlyList<string> MusicList => _media.MusicList;

    public void PauseMusic()
    {
        _media.PauseMusic();
    }

    public void ResumeMusic()
    {
        _media.ResumeMusic();
    }

    public void ToggleMusic()
    {
        _media.ToggleMusic();
    }

    public void PlayNextMusic()
    {
        _media.PlayNextMusic();
    }

    public void RefreshMusic(bool showHint, bool isFirstLoad = false)
    {
        _media.RefreshMusic(showHint, isFirstLoad);
    }

    public void ClearMusic()
    {
        _media.ClearMusic();
    }

    public void SetMusicVolume(int volume)
    {
        _media.SetMusicVolume(volume);
    }

    /// <summary>右下角音乐快捷按钮的展示状态；没有音乐时返回 <see langword="null" />。</summary>
    public ExtraButtonsPanel.MusicButtonState? GetMusicButtonState()
    {
        if (!_media.IsMusicVisible) return null;

        var playing = _media.MusicState == MusicPlaybackState.Play;
        var paused = _media.MusicState == MusicPlaybackState.Pause;
        var single = _media.MusicCount == 1;
        var name = Path.GetFileNameWithoutExtension(_media.CurrentMusicPath ?? string.Empty);
        var tipKey = paused
            ? single
                ? "Music.ToolTip.Paused.Single"
                : "Music.ToolTip.Paused.Multiple"
            : single
                ? "Music.ToolTip.Playing.Single"
                : "Music.ToolTip.Playing.Multiple";
        return new ExtraButtonsPanel.MusicButtonState(
            true,
            playing,
            Lang.Text(tipKey, name));
    }
}