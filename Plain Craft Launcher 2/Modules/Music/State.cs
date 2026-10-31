using NAudio.Wave;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;

namespace PCL;

public static partial class Music
{
    public enum MusicStates
    {
        Stop,
        Play,
        Pause
    }

    public static MusicStates MusicState
    {
        get
        {
            return musicNAudio is null
                ? MusicStates.Stop
                : musicNAudio.PlaybackState switch
                {
                    PlaybackState.Paused => MusicStates.Pause,
                    PlaybackState.Stopped => MusicStates.Stop,
                    _ => MusicStates.Play
                };
        }
    }

    public static void MusicRefreshPlay(bool showHint, bool isFirstLoad = false)
    {
        try
        {
            MusicListInit(true);

            if (musicAllList?.Any() == false)
            {
                if (musicNAudio is not null)
                {
                    musicNAudio = null;
                    if (showHint)
                        HintService.Show(Hints.Success(Lang.Text("Music.Hint.Cleared")));
                }
                else if (showHint)
                {
                    HintService.Show(Hints.Error(Lang.Text("Music.Error.NotDetected")));
                }
            }
            else
            {
                var addr = DequeueNextMusicAddress();
                if (addr is null)
                {
                    if (showHint)
                        HintService.Show(Hints.Error(Lang.Text("Music.Error.NoAvailable")));
                }
                else
                {
                    try
                    {
                        MusicStartPlay(addr, isFirstLoad);
                        if (showHint)
                            HintService.Show(Hints.Success(Lang.Text("Music.Hint.Refreshed", Common.GetFileNameFromPath(addr)), log: false));
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            MusicRefreshUi();
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                "刷新背景音乐播放失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Music.Error.OperationFailed"));
        }
    }

    private static void MusicStartPlay(string address, bool isFirstLoad = false)
    {
        if (string.IsNullOrEmpty(address))
            return;
        Common.Log("[Music] 播放开始：" + address);
        _musicCurrent = address;
        Common.RunInNewThread(() => MusicLoop(isFirstLoad), "Music", ThreadPriority.BelowNormal);
    }

    public static bool MusicPause()
    {
        if (MusicState != MusicStates.Play)
        {
            Common.Log($"[Music] 无需暂停播放，当前状态为 {MusicState}");
            return false;
        }

        Common.RunInThread(() =>
        {
            Common.Log("[Music] 已暂停播放");
            musicNAudio?.Pause();
            MusicRefreshUi();
        });
        return true;
    }

    public static bool MusicResume()
    {
        if (MusicState == MusicStates.Play || musicAllList is null || musicAllList.Count == 0)
        {
            Common.Log($"[Music] 无需继续播放，当前状态为 {MusicState}");
            return false;
        }

        Common.RunInThread(() =>
        {
            Common.Log("[Music] 已恢复播放");
            try
            {
                musicNAudio?.Play();
            }
            catch
            {
                musicNAudio?.Stop();
                musicNAudio?.Play();
            }

            MusicRefreshUi();
        });
        return true;
    }
}