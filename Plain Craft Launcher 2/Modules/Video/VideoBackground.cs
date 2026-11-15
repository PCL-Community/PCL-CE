namespace PCL;

public static class VideoBackground
{
    public static bool isMinimized = false;

    public static bool IsGaming
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            GamingStateChanged?.Invoke(null, new BooleanEventArgs(value));
        }
    }

    public static bool ForcePlay
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            ForcePlayChanged?.Invoke(null, new BooleanEventArgs(value));
        }
    }

    public static event EventHandler<BooleanEventArgs>? GamingStateChanged;
    public static event EventHandler<BooleanEventArgs>? ForcePlayChanged;

    public static void OnGamingStateChanged(object sender, BooleanEventArgs e)
    {
        Common.RunInUi(() =>
        {
            if (IsGaming)
            {
                if (ForcePlay)
                {
                    if (Launcher.frmSetupUi is not null)
                        Launcher.frmSetupUi.BtnBackgroundRefresh.IsEnabled = true;
                }
                else if (Launcher.frmSetupUi is not null)
                {
                    Launcher.frmSetupUi.BtnBackgroundRefresh.IsEnabled = false;
                }
            }
            else if (Launcher.frmSetupUi is not null)
            {
                Launcher.frmSetupUi.BtnBackgroundRefresh.IsEnabled = true;
            }
        });
    }

    public static void OnForcePlayChanged(object sender, BooleanEventArgs e)
    {
        Common.RunInUi(() =>
        {
            if (IsGaming)
            {
                if (ForcePlay)
                {
                    if (Launcher.frmSetupUi is not null)
                        Launcher.frmSetupUi.BtnBackgroundRefresh.IsEnabled = true;
                }
                else if (Launcher.frmSetupUi is not null)
                {
                    Launcher.frmSetupUi.BtnBackgroundRefresh.IsEnabled = false;
                }
            }
            else if (Launcher.frmSetupUi is not null)
            {
                Launcher.frmSetupUi.BtnBackgroundRefresh.IsEnabled = true;
            }
        });
    }

    /// <summary>
    ///     尝试开始视频背景播放
    /// </summary>
    public static void VideoPlay()
    {
        Common.RunInUi(() =>
        {
            if (Launcher.frmMain?.VideoBack.Source is null || isMinimized) return;
            if (IsGaming && !ForcePlay) return;

            try
            {
                Launcher.frmMain.VideoBack.Play();
                Common.Log("[UI] 已开始视频背景播放");
            }
            catch (Exception ex)
            {
                Common.Log(ex, "[UI] 开始视频背景播放失败");
            }
        });
    }

    /// <summary>
    ///     尝试停止视频背景播放
    /// </summary>
    public static void VideoStop()
    {
        Common.RunInUi(() =>
        {
            if (Launcher.frmMain is null)
                return;

            try
            {
                Launcher.frmMain.VideoBack.Source = null;
                Launcher.frmMain.VideoBack.Stop();
                Launcher.frmMain.VideoBack.Position = TimeSpan.Zero;
                Common.Log("[UI] 已停止视频背景播放");
            }
            catch (Exception ex)
            {
                Common.Log(ex, "[UI] 停止视频背景播放失败");
            }
        });
    }

    /// <summary>
    ///     尝试暂停视频背景播放
    /// </summary>
    public static void VideoPause()
    {
        Common.RunInUi(() =>
        {
            if (isMinimized)
            {
                if (Launcher.frmMain?.VideoBack.Source is null) return;

                try
                {
                    Launcher.frmMain.VideoBack.Pause();
                    Common.Log("[UI] 已暂停视频背景播放");
                }
                catch (Exception ex)
                {
                    Common.Log(ex, "[UI] 暂停视频背景播放失败");
                }
            }
            else if (Launcher.frmMain?.VideoBack.Source is not null)
            {
                try
                {
                    if (Launcher.frmSetupUi is not null)
                        Launcher.frmSetupUi.BtnBackgroundRefresh.IsEnabled = false;
                    Launcher.frmMain.VideoBack.Pause();
                    Common.Log("[UI] 已暂停视频背景播放");
                }
                catch (Exception ex)
                {
                    Common.Log(ex, "[UI] 暂停视频背景播放失败");
                }
            }
        });
    }

    public class BooleanEventArgs(bool value) : EventArgs
    {
        public bool Value { get; set; } = value;
    }
}