namespace PCL;

public static class ModVideoBack
{
    private static bool _isGaming;
    private static bool _forcePlay;
    public static bool IsMinimized = false; // 窗口是否被最小化

    public static bool IsGaming // 判断用户是否在游戏中
    {
        get => _isGaming;
        set
        {
            if (_isGaming != value)
            {
                _isGaming = value;
                GamingStateChanged?.Invoke(null, new BooleanEventArgs(value));
            }
        }
    }

    public static bool ForcePlay // 判断是否强行播放
    {
        get => _forcePlay;
        set
        {
            if (_forcePlay != value)
            {
                _forcePlay = value;
                ForcePlayChanged?.Invoke(null, new BooleanEventArgs(value));
            }
        }
    }

    public static event EventHandler<BooleanEventArgs>? GamingStateChanged;
    public static event EventHandler<BooleanEventArgs>? ForcePlayChanged;

    public static void OnGamingStateChanged(object sender, BooleanEventArgs e) // 用户是否在游戏中 事件
    {
        ModBase.RunInUi(() =>
        {
            if (IsGaming)
            {
                if (ForcePlay)
                {
                    if (!(ModMain.FrmSetupUI == null)) ModMain.FrmSetupUI.BtnBackgroundRefresh.IsEnabled = true;
                }
                else if (!(ModMain.FrmSetupUI == null))
                {
                    ModMain.FrmSetupUI.BtnBackgroundRefresh.IsEnabled = false;
                }
            }
            else if (!(ModMain.FrmSetupUI == null))
            {
                ModMain.FrmSetupUI.BtnBackgroundRefresh.IsEnabled = true;
            }
        });
    }

    public static void OnForcePlayChanged(object sender, BooleanEventArgs e) // 是否强行播放 事件
    {
        ModBase.RunInUi(() =>
        {
            if (IsGaming)
            {
                if (ForcePlay)
                {
                    if (!(ModMain.FrmSetupUI == null)) ModMain.FrmSetupUI.BtnBackgroundRefresh.IsEnabled = true;
                }
                else if (!(ModMain.FrmSetupUI == null))
                {
                    ModMain.FrmSetupUI.BtnBackgroundRefresh.IsEnabled = false;
                }
            }
            else if (!(ModMain.FrmSetupUI == null))
            {
                ModMain.FrmSetupUI.BtnBackgroundRefresh.IsEnabled = true;
            }
        });
    }

    /// <summary>
    ///     尝试开始视频背景播放
    /// </summary>
    public static void VideoPlay()
    {
        ModBase.RunInUi(() =>
        {
            if (ModMain.FrmMain.VideoBack.Source is not null && !IsMinimized)
                if (!IsGaming || ForcePlay)
                    try
                    {
                        ModMain.FrmMain.VideoBack.Play();
                        ModBase.Log("[UI] 已开始视频背景播放");
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "[UI] 开始视频背景播放失败");
                    }
        });
    }

    /// <summary>
    ///     尝试停止视频背景播放
    /// </summary>
    public static void VideoStop()
    {
        ModBase.RunInUi(() =>
        {
            try
            {
                ModMain.FrmMain.VideoBack.Source = null;
                ModMain.FrmMain.VideoBack.Stop();
                ModMain.FrmMain.VideoBack.Position = TimeSpan.Zero;
                ModBase.Log("[UI] 已停止视频背景播放");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[UI] 停止视频背景播放失败");
            }
        });
    }

    /// <summary>
    ///     尝试暂停视频背景播放
    /// </summary>
    public static void VideoPause()
    {
        // 窗口最小化后暂停
        // 游戏启动后暂停
        ModBase.RunInUi(() =>
        {
            if (IsMinimized)
            {
                if (ModMain.FrmMain.VideoBack.Source is not null)
                    try
                    {
                        ModMain.FrmMain.VideoBack.Pause();
                        ModBase.Log("[UI] 已暂停视频背景播放");
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "[UI] 暂停视频背景播放失败");
                    }
            }
            else if (ForcePlay)
            {
            }
            else if (ModMain.FrmMain.VideoBack.Source is not null)
            {
                try
                {
                    if (!(ModMain.FrmSetupUI == null)) ModMain.FrmSetupUI.BtnBackgroundRefresh.IsEnabled = false;
                    ModMain.FrmMain.VideoBack.Pause();
                    ModBase.Log("[UI] 已暂停视频背景播放");
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "[UI] 暂停视频背景播放失败");
                }
            }
        });
    }

    public class BooleanEventArgs : EventArgs
    {
        public BooleanEventArgs(bool value)
        {
            Value = value;
        }

        public bool Value { get; set; }
    }
}
