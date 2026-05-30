using System.IO;
using System.Windows.Controls;
using NAudio;
using NAudio.Wave;
using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.Utils;

namespace PCL;

public static class ModMusic
{
    /// <summary>
    ///     当前正在播放的 NAudio.Wave.WaveOutEvent。
    /// </summary>
    public static WaveOutEvent MusicNAudio;

    /// <summary>
    ///     当前播放的音乐地址。
    /// </summary>
    private static string MusicCurrent = "";

    private static void MusicLoop(bool IsFirstLoad = false)
    {
        WaveOutEvent currentWave = null;
        AudioFileReader reader = null;

        try
        {
            currentWave = new WaveOutEvent();
            MusicNAudio = currentWave;
            currentWave.DeviceNumber = -1; // 使用默认设备

            reader = new AudioFileReader(MusicCurrent);
            currentWave.Init(reader);
            currentWave.Play();

            // 首次加载且用户未启用自动播放，则暂停
            if (IsFirstLoad && !Config.Preference.Music.StartOnStartup) currentWave.Pause();

            MusicRefreshUI();

            var lastVolume = Config.Preference.Music.Volume;
            currentWave.Volume = lastVolume / 1000.0f;

            // 播放主循环
            while (currentWave.Equals(MusicNAudio) && currentWave.PlaybackState != PlaybackState.Stopped)
            {
                // 音量动态更新
                var currentVolume = Config.Preference.Music.Volume;
                if (currentVolume != lastVolume)
                {
                    lastVolume = currentVolume;
                    currentWave.Volume = currentVolume / 1000.0f;
                }

                // 更新进度条
                if (reader.TotalTime.TotalMilliseconds > 0d)
                {
                    var progress = reader.CurrentTime.TotalMilliseconds / reader.TotalTime.TotalMilliseconds;
                    ModBase.RunInUi(() => ModMain.FrmMain.BtnExtraMusic.Progress = progress);
                }

                Thread.Sleep(100);
            }

            // 播放结束，继续下一首
            if (currentWave.PlaybackState == PlaybackState.Stopped &&
                (MusicAllList?.Any() is { } arg5 ? arg5 : (bool?)null).GetValueOrDefault())
                MusicStartPlay(DequeueNextMusicAddress(), IsFirstLoad);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, $"播放音乐出现内部错误（{MusicCurrent}）", ModBase.LogLevel.Developer);

            // 错误处理：精准提示用户
            var fileName = ModBase.GetFileNameFromPath(MusicCurrent);
            if (ex is MmException)
            {
                var msg = ex.Message;
                if (msg.Contains("AlreadyAllocated"))
                    ModMain.Hint("你的音频设备正被其他程序占用。请关闭占用程序后重启 PCL 以恢复音乐功能！", ModMain.HintType.Critical);
                else if (msg.Contains("NoDriver") || msg.Contains("BadDeviceId"))
                    ModMain.Hint("音频设备发生变更，音乐播放功能需重启 PCL 后恢复！", ModMain.HintType.Critical);
                else
                    ModBase.Log(ex, $"播放失败（{fileName}）", ModBase.LogLevel.Hint);
            }
            else if (ex.Message.Contains("Got a frame at sample rate") ||
                     ex.Message.Contains("does not support changes to"))
            {
                ModMain.Hint($"播放失败（{fileName}）：PCL 不支持中途变更音频属性的音乐文件", ModMain.HintType.Critical);
            }
            else if ((!MusicCurrent.EndsWithF(".wav", true) && !MusicCurrent.EndsWithF(".mp3", true) &&
                      !MusicCurrent.EndsWithF(".flac", true)) || ex.Message.Contains("0xC00D36C4"))
            {
                ModMain.Hint($"播放失败（{fileName}）：PCL 可能不支持此格式，请转换为 .wav/.mp3/.flac", ModMain.HintType.Critical);
            }
            else
            {
                ModBase.Log(ex, $"播放失败（{fileName}）", ModBase.LogLevel.Hint);
            }

            // 移除无效文件
            MusicAllList?.Remove(MusicCurrent);
            MusicWaitingList?.Remove(MusicCurrent);
            MusicRefreshUI();

            Thread.Sleep(2000);

            // 尝试播放下一首
            if (ex is FileNotFoundException)
                MusicRefreshPlay(true, IsFirstLoad);
            else
                MusicStartPlay(DequeueNextMusicAddress(), IsFirstLoad);
        }
        finally
        {
            currentWave?.Dispose();
            reader?.Dispose();
            MusicRefreshUI();
        }
    }

    #region 播放列表

    /// <summary>
    ///     接下来要播放的音乐文件路径。未初始化时为 Nothing。
    /// </summary>
    public static List<string> MusicWaitingList;

    /// <summary>
    ///     全部音乐文件路径。未初始化时为 Nothing。
    /// </summary>
    public static List<string> MusicAllList;

    /// <summary>
    ///     初始化音乐播放列表。
    /// </summary>
    /// <param name="ForceReload">强制全部重新载入列表。</param>
    /// <param name="PreventFirst">在重载列表时避免让某项成为第一项。</param>
    private static void MusicListInit(bool ForceReload, string PreventFirst = null)
    {
        if (ForceReload)
            MusicAllList = null;

        try
        {
            if (MusicAllList is null)
            {
                MusicAllList = new List<string>();
                var musicDir = Path.Combine(ModBase.ExePath, "PCL", "Musics");
                Directory.CreateDirectory(musicDir);
                foreach (var file in ModBase.EnumerateFiles(musicDir))
                {
                    var ext = file.Extension.ToLowerInvariant();
                    // 忽略非音频文件
                    if (new[] { ".ini", ".jpg", ".txt", ".cfg", ".lrc", ".db", ".png" }.Contains(ext))
                        continue;
                    MusicAllList.Add(file.FullName);
                }
            }

            // 根据设置决定是否随机
            if (Config.Preference.Music.ShufflePlayback)
                MusicWaitingList = RandomUtils.Shuffle(new List<string>(MusicAllList));
            else
                MusicWaitingList = new List<string>(MusicAllList);

            // 避免 PreventFirst 成为第一项
            if (PreventFirst is not null && MusicWaitingList.Count > 0 && string.Equals(MusicWaitingList[0],
                    PreventFirst, StringComparison.OrdinalIgnoreCase))
            {
                MusicWaitingList.RemoveAt(0);
                MusicWaitingList.Add(PreventFirst);
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化音乐列表失败", ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     获取下一首播放的音乐路径并将其从列表中移除。
    ///     如果没有，可能会返回 Nothing。
    /// </summary>
    private static string DequeueNextMusicAddress()
    {
        if (MusicAllList is null || !MusicAllList.Any() || !MusicWaitingList.Any()) MusicListInit(false);

        if (MusicWaitingList.Any())
        {
            var nextMusic = MusicWaitingList[0];
            MusicWaitingList.RemoveAt(0);
            if (!MusicWaitingList.Any()) MusicListInit(false, nextMusic);
            return nextMusic;
        }

        return null;
    }

    #endregion

    #region UI 控制

    /// <summary>
    ///     刷新背景音乐按钮 UI 与设置页 UI。
    /// </summary>
    private static void MusicRefreshUI()
    {
        ModBase.RunInUi(() =>
        {
            try
            {
                if ((MusicAllList?.Any() is { } arg1 ? arg1 : null) == false)
                {
                    ModMain.FrmMain.BtnExtraMusic.Show = false;
                }
                else
                {
                    ModMain.FrmMain.BtnExtraMusic.Show = true;
                    var fileName = ModBase.GetFileNameWithoutExtentionFromPath(MusicCurrent);
                    var isSingle = MusicAllList.Count == 1;
                    string tipText;
                    if (MusicState == MusicStates.Pause)
                    {
                        ModMain.FrmMain.BtnExtraMusic.Logo = Icon.IconPlay;
                        ModMain.FrmMain.BtnExtraMusic.LogoScale = 0.8d;
                        tipText = $"已暂停：{fileName}";
                        tipText += "\r\n" + (isSingle ? "左键恢复播放，右键重新从头播放。" : "左键恢复播放，右键播放下一曲。");
                    }
                    else
                    {
                        ModMain.FrmMain.BtnExtraMusic.Logo = Icon.IconMusic;
                        ModMain.FrmMain.BtnExtraMusic.LogoScale = 1d;
                        tipText = $"正在播放：{fileName}";
                        tipText += "\r\n" + (isSingle ? "左键暂停，右键重新从头播放。" : "左键暂停，右键播放下一曲。");
                    }

                    ModMain.FrmMain.BtnExtraMusic.ToolTip = tipText;
                    ToolTipService.SetVerticalOffset(ModMain.FrmMain.BtnExtraMusic,
                        tipText.Contains("\n") ? 10 : 16);
                }

                ModMain.FrmSetupUI?.MusicRefreshUI();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "刷新背景音乐 UI 失败", ModBase.LogLevel.Feedback);
            }
        });
    }

    public static void MusicControlPause()
    {
        if (MusicNAudio is null)
        {
            ModMain.Hint("音乐播放尚未开始！", ModMain.HintType.Critical);
            return;
        }

        switch (MusicState)
        {
            case MusicStates.Pause:
            {
                MusicResume();
                break;
            }
            case MusicStates.Play:
            {
                MusicPause(); // Stop
                break;
            }

            default:
            {
                ModBase.Log("[Music] 音乐目前为停止状态，已强制尝试开始播放", ModBase.LogLevel.Debug);
                MusicRefreshPlay(false);
                break;
            }
        }
    }

    public static void MusicControlNext()
    {
        if (MusicAllList?.Count is { } arg2 && arg2 == 1)
        {
            MusicStartPlay(MusicCurrent);
            ModMain.Hint("重新播放：" + ModBase.GetFileNameFromPath(MusicCurrent), ModMain.HintType.Finish);
        }
        else
        {
            var addr = DequeueNextMusicAddress();
            if (addr is null)
            {
                ModMain.Hint("没有可以播放的音乐！", ModMain.HintType.Critical);
            }
            else
            {
                MusicStartPlay(addr);
                ModMain.Hint("正在播放：" + ModBase.GetFileNameFromPath(addr), ModMain.HintType.Finish);
            }
        }

        MusicRefreshUI();
    }

    #endregion

    #region 主状态控制

    public static MusicStates MusicState
    {
        get
        {
            if (MusicNAudio is null)
                return MusicStates.Stop;
            return MusicNAudio.PlaybackState == PlaybackState.Paused ? MusicStates.Pause :
                MusicNAudio.PlaybackState == PlaybackState.Stopped ? MusicStates.Stop : MusicStates.Play;
        }
    }

    public enum MusicStates
    {
        Stop,
        Play,
        Pause
    }

    public static void MusicRefreshPlay(bool ShowHint, bool IsFirstLoad = false)
    {
        try
        {
            MusicListInit(true);

            if ((MusicAllList?.Any() is { } arg3 ? arg3 : null) == false)
            {
                if (MusicNAudio is not null)
                {
                    MusicNAudio = null;
                    if (ShowHint)
                        ModMain.Hint("背景音乐已清除！", ModMain.HintType.Finish);
                }
                else if (ShowHint)
                {
                    ModMain.Hint("未检测到可用的背景音乐！", ModMain.HintType.Critical);
                }
            }
            else
            {
                var addr = DequeueNextMusicAddress();
                if (addr is null)
                {
                    if (ShowHint)
                        ModMain.Hint("没有可以播放的音乐！", ModMain.HintType.Critical);
                }
                else
                {
                    try
                    {
                        MusicStartPlay(addr, IsFirstLoad);
                        if (ShowHint)
                            ModMain.Hint("背景音乐已刷新：" + ModBase.GetFileNameFromPath(addr), ModMain.HintType.Finish,
                                false);
                    }
                    catch
                    {
                        // 容错：播放失败已在 MusicLoop 中处理
                    }
                }
            }

            MusicRefreshUI();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新背景音乐播放失败", ModBase.LogLevel.Feedback);
        }
    }

    private static void MusicStartPlay(string Address, bool IsFirstLoad = false)
    {
        if (string.IsNullOrEmpty(Address))
            return;
        ModBase.Log("[Music] 播放开始：" + Address);
        MusicCurrent = Address;
        ModBase.RunInNewThread(() => MusicLoop(IsFirstLoad), "Music", ThreadPriority.BelowNormal);
    }

    public static bool MusicPause()
    {
        if (MusicState != MusicStates.Play)
        {
            ModBase.Log($"[Music] 无需暂停播放，当前状态为 {MusicState}");
            return false;
        }

        ModBase.RunInThread(() =>
        {
            ModBase.Log("[Music] 已暂停播放");
            MusicNAudio?.Pause();
            MusicRefreshUI();
        });
        return true;
    }

    public static bool MusicResume()
    {
        if (MusicState == MusicStates.Play || MusicAllList.Count == 0)
        {
            ModBase.Log($"[Music] 无需继续播放，当前状态为 {MusicState}");
            return false;
        }

        ModBase.RunInThread(() =>
        {
            ModBase.Log("[Music] 已恢复播放");
            try
            {
                MusicNAudio?.Play();
            }
            catch
            {
                // 参考 PR #5415：设备变更后需 Stop + Play
                MusicNAudio?.Stop();
                MusicNAudio?.Play();
            }

            MusicRefreshUI();
        });
        return true;
    }

    #endregion
}
