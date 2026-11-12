using System.IO;
using NAudio;
using NAudio.Wave;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;

namespace PCL;

public static partial class Music
{
    /// <summary>
    ///     当前正在播放的 NAudio.Wave.WaveOutEvent。
    /// </summary>
    public static WaveOutEvent? musicNAudio;

    /// <summary>
    ///     当前播放的音乐地址。
    /// </summary>
    private static string _musicCurrent = "";

    private static void MusicLoop(bool isFirstLoad = false)
    {
        WaveOutEvent? currentWave = null;
        AudioFileReader? reader = null;

        try
        {
            currentWave = new WaveOutEvent();
            musicNAudio = currentWave;
            currentWave.DeviceNumber = -1;

            reader = new AudioFileReader(_musicCurrent);
            currentWave.Init(reader);
            currentWave.Play();

            if (isFirstLoad && !Config.Preference.Music.StartOnStartup)
                currentWave.Pause();

            MusicRefreshUi();

            var lastVolume = Config.Preference.Music.Volume;
            currentWave.Volume = lastVolume / 1000.0f;

            _RunPlaybackLoop(currentWave, reader, lastVolume);

            if (currentWave.PlaybackState == PlaybackState.Stopped && musicAllList?.Any() == true)
            {
                var nextAddress = DequeueNextMusicAddress();
                if (nextAddress is not null)
                    MusicStartPlay(nextAddress, isFirstLoad);
            }
        }
        catch (Exception ex)
        {
            _HandlePlaybackError(ex, isFirstLoad);
        }
        finally
        {
            currentWave?.Dispose();
            reader?.Dispose();
            MusicRefreshUi();
        }
    }

    private static void _RunPlaybackLoop(
        WaveOutEvent currentWave,
        AudioFileReader reader,
        int initialVolume)
    {
        var lastVolume = initialVolume;

        while (currentWave.Equals(musicNAudio)
               && currentWave.PlaybackState != PlaybackState.Stopped)
        {
            var currentVolume = Config.Preference.Music.Volume;
            if (currentVolume != lastVolume)
            {
                lastVolume = currentVolume;
                currentWave.Volume = currentVolume / 1000.0f;
            }

            if (reader.TotalTime.TotalMilliseconds > 0d)
            {
                var progress = reader.CurrentTime.TotalMilliseconds
                    / reader.TotalTime.TotalMilliseconds;
                Common.RunInUi(() => Launcher.frmMain?.BtnExtraMusic.Progress = progress);
            }

            Thread.Sleep(100);
        }
    }

    private static void _HandlePlaybackError(Exception ex, bool isFirstLoad)
    {
        Common.Log(ex, $"播放音乐出现内部错误（{_musicCurrent}）", Common.LogLevel.Developer);

        var fileName = Common.GetFileNameFromPath(_musicCurrent);
        switch (ex)
        {
            case MmException:
            {
                var msg = ex.Message;
                if (msg.Contains("AlreadyAllocated"))
                    HintService.Show(Hints.Error(Lang.Text("Music.Error.DeviceBusy")));
                else if (msg.Contains("NoDriver") || msg.Contains("BadDeviceId"))
                    HintService.Show(Hints.Error(Lang.Text("Music.Error.DeviceChanged")));
                else
                    _LogPlaybackFailed(ex, fileName);
                break;
            }

            default:
            {
                if (ex.Message.Contains("Got a frame at sample rate")
                    || ex.Message.Contains("does not support changes to"))
                    HintService.Show(Hints.Error(Lang.Text("Music.Error.PropertyChangeUnsupported", fileName)));
                else if (_IsUnsupportedMusicFormat() || ex.Message.Contains("0xC00D36C4"))
                    HintService.Show(Hints.Error(Lang.Text("Music.Error.UnsupportedFormat", fileName)));
                else
                    _LogPlaybackFailed(ex, fileName);
                break;
            }
        }

        musicAllList?.Remove(_musicCurrent);
        musicWaitingList?.Remove(_musicCurrent);
        MusicRefreshUi();

        Thread.Sleep(2000);

        if (ex is FileNotFoundException)
        {
            MusicRefreshPlay(true, isFirstLoad);
        }
        else
        {
            var nextAddress = DequeueNextMusicAddress();
            if (nextAddress is not null)
                MusicStartPlay(nextAddress, isFirstLoad);
        }
    }

    private static bool _IsUnsupportedMusicFormat()
    {
        return !_musicCurrent.EndsWithF(".wav", true)
            && !_musicCurrent.EndsWithF(".mp3", true)
            && !_musicCurrent.EndsWithF(".flac", true);
    }

    private static void _LogPlaybackFailed(Exception ex, string fileName)
    {
        Common.Log(
            ex,
            $"播放失败（{fileName}）",
            Common.LogLevel.Hint,
            userSummary: Lang.Text("Music.Error.PlaybackFailed", fileName));
    }
}