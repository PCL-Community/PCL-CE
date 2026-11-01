using System.IO;
using System.Windows.Controls;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;

namespace PCL;

public static partial class Music
{
    /// <summary>
    ///     刷新背景音乐按钮 UI 与设置页 UI。
    /// </summary>
    private static void MusicRefreshUi()
    {
        Common.RunInUi(() =>
        {
            try
            {
                if (Launcher.frmMain is null)
                    return;

                if (musicAllList is null || musicAllList.Count == 0)
                {
                    Launcher.frmMain.BtnExtraMusic.Show = false;
                }
                else
                {
                    Launcher.frmMain.BtnExtraMusic.Show = true;
                    var fileName = Path.GetFileNameWithoutExtension(_musicCurrent);
                    var isSingle = musicAllList.Count == 1;
                    string tipText;
                    if (MusicState == MusicStates.Pause)
                    {
                        Launcher.frmMain.BtnExtraMusic.SvgIcon = "lucide/play";
                        Launcher.frmMain.BtnExtraMusic.LogoScale = 0.8d;
                        tipText = Lang.Text(
                            isSingle
                                ? "Music.ToolTip.Paused.Single"
                                : "Music.ToolTip.Paused.Multiple",
                            fileName);
                    }
                    else
                    {
                        Launcher.frmMain.BtnExtraMusic.SvgIcon = "lucide/music";
                        Launcher.frmMain.BtnExtraMusic.LogoScale = 1d;
                        tipText = Lang.Text(
                            isSingle
                                ? "Music.ToolTip.Playing.Single"
                                : "Music.ToolTip.Playing.Multiple",
                            fileName);
                    }

                    Launcher.frmMain.BtnExtraMusic.ToolTip = tipText;
                    ToolTipService.SetVerticalOffset(
                        Launcher.frmMain.BtnExtraMusic,
                        tipText.Contains("\n") ? 10 : 16);
                }

                Launcher.frmSetupUi?.MusicRefreshUI();
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    "刷新背景音乐 UI 失败",
                    Common.LogLevel.Feedback,
                    userSummary: Lang.Text("Music.Error.OperationFailed"));
            }
        });
    }

    public static void MusicControlPause()
    {
        if (musicNAudio is null)
        {
            HintService.Show(Hints.Error(Lang.Text("Music.Error.NotStarted")));
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
                MusicPause();
                break;
            }

            case MusicStates.Stop:
            default:
            {
                Common.Log("[Music] 音乐目前为停止状态，已强制尝试开始播放", Common.LogLevel.Debug);
                MusicRefreshPlay(false);
                break;
            }
        }
    }

    public static void MusicControlNext()
    {
        if (musicAllList?.Count is 1)
        {
            MusicStartPlay(_musicCurrent);
            HintService.Show(Hints.Success(Lang.Text("Music.Hint.Replaying", Common.GetFileNameFromPath(_musicCurrent))));
        }
        else
        {
            var addr = DequeueNextMusicAddress();
            if (addr is null)
            {
                HintService.Show(Hints.Error(Lang.Text("Music.Error.NoAvailable")));
            }
            else
            {
                MusicStartPlay(addr);
                HintService.Show(Hints.Success(Lang.Text("Music.Hint.Playing", Common.GetFileNameFromPath(addr))));
            }
        }

        MusicRefreshUi();
    }
}