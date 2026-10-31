using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;

namespace PCL;

public partial class PageSetupUI : IPageView
{
    private readonly PageSetupUIViewModel _vm = new();

    private bool _refreshed;

    public PageSetupUIViewModel ViewModel => _vm;

    public string[] ThemeColors => _vm.ThemeColors;

    public new bool isLoaded;

    public PageSetupUI()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += PageSetupUI_Loaded;
        Loaded += (_, _) => HiddenRefresh();
        Unloaded += (_, _) => _refreshed = false;
    }

    private void PageSetupUI_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshOnNavigate();

        // 非重复加载部分
        if (isLoaded)
            return;
        isLoaded = true;

        SliderLoad();

        _vm.ShowLauncherHint();
    }

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        // 重复加载部分
        PanBack.ScrollToHome();

        Animations.AniBeginControl();
        Reload(); // #4826，在每次进入页面时都刷新一下
        Animations.AniEndControl();
    }

    public void Reload()
    {
        try
        {
            // 启动器
            SliderLauncherOpacity.Value = Config.Preference.Theme.WindowOpacity;
            CheckLauncherLogo.Checked = Config.Preference.ShowStartupLogo;
            _vm.DarkModeSelectedIndex = (int)Config.Preference.Theme.ColorMode;
            _vm.DarkColorSelectedIndex = (int)Config.Preference.Theme.DarkColor;
            _vm.LightColorSelectedIndex = (int)Config.Preference.Theme.LightColor;
            CheckShowLaunchingHint.Checked = Config.Preference.ShowLaunchingHint;

            // 字体设置
            ComboUiFont.SelectedFontTag = Config.Preference.Font;
            ComboUiMotdFont.SelectedFontTag = Config.Preference.MotdFont;

            CheckBlur.Checked = Config.Preference.Blur.IsEnabled;
            SliderBlurValue.Value = Config.Preference.Blur.Radius;
            SliderBlurSamplingRate.Value = Config.Preference.Blur.SamplingRate;
            _vm.BlurTypeSelectedIndex = Config.Preference.Blur.KernelType;
            PanBlurValue.Visibility = CheckBlur.Checked == true ? Visibility.Visible : Visibility.Collapsed;
            CheckLockWindowSize.Checked = Config.Preference.LockWindowSize;

            // 背景图片
            SliderBackgroundOpacity.Value = Config.Preference.Background.WallpaperOpacity;
            SliderBackgroundBlur.Value = Config.Preference.Background.WallpaperBlurRadius;
            _vm.BackgroundSuitSelectedIndex = Config.Preference.Background.WallpaperSuitMode;
            CheckBackgroundColorful.Checked = Config.Preference.Background.BackgroundColorful;
            var autoPauseVideo = Config.Preference.Background.AutoPauseVideo;
            CheckAutoPauseVideo.Checked = autoPauseVideo;
            if (VideoBackground.IsGaming)
                if (autoPauseVideo)
                    BtnBackgroundRefresh.IsEnabled = false;

            BackgroundRefresh(false, false);

            // 标题栏
            ((MyRadioBox)FindName("RadioLogoType" + (int)Config.Preference.WindowTitleType))
                .Checked = true;
            CheckLogoLeft.Visibility = RadioLogoType0.Checked ? Visibility.Visible : Visibility.Collapsed;
            PanLogoText.Visibility = RadioLogoType2.Checked ? Visibility.Visible : Visibility.Collapsed;
            PanLogoChange.Visibility = RadioLogoType3.Checked ? Visibility.Visible : Visibility.Collapsed;
            _vm.LogoText = Config.Preference.WindowTitleCustomText;
            CheckLogoLeft.Checked = Config.Preference.TopBarLeftAlign;

            // 背景音乐
            CheckMusicRandom.Checked = Config.Preference.Music.ShufflePlayback;
            CheckMusicAuto.Checked = Config.Preference.Music.StartOnStartup;
            CheckMusicStop.Checked = Config.Preference.Music.StopInGame;
            CheckMusicStart.Checked = Config.Preference.Music.StartInGame;
            CheckMusicSMTC.Checked = Config.Preference.Music.EnableSMTC;
            SliderMusicVolume.Value = Config.Preference.Music.Volume;
            MusicRefreshUI();

            // 主页
            try
            {
                _vm.CustomPresetSelectedIndex = Config.Preference.Homepage.SelectedPreset;
            }
            catch
            {
                Config.Preference.Homepage.SelectedPresetConfig.Reset();
            }

            ((MyRadioBox)FindName("RadioCustomType" + Config.Preference.Homepage.Type)).Checked = true;
            _vm.CustomNetText = Config.Preference.Homepage.CustomUrl;
            Setup.UiCustomType(Config.Preference.Homepage.Type);

            // 功能隐藏
            // 获取配置组引用
            var uiHidden = Config.Preference.Hide;

            // 主页面
            CheckHiddenPageDownload.Checked = uiHidden.PageDownload;
            CheckHiddenPageSetup.Checked = uiHidden.PageSetup;
            CheckHiddenPageTools.Checked = uiHidden.PageTools;

            // 子页面 设置
            CheckHiddenSetupLaunch.Checked = uiHidden.SetupLaunch;
            CheckHiddenSetupUI.Checked = uiHidden.SetupUi;
            CheckHiddenSetupLauncherLanguage.Checked = uiHidden.SetupLauncherLanguage;
            CheckHiddenSetupGameManage.Checked = uiHidden.SetupGameManage;
            CheckHiddenSetupJava.Checked = uiHidden.SetupJava;
            CheckHiddenLauncherMisc.Checked = uiHidden.SetupLauncherMisc;
            CheckHiddenSetupUpdate.Checked = uiHidden.SetupUpdate;
            CheckHiddenSetupGameLink.Checked = uiHidden.SetupGameLink;
            CheckHiddenSetupAbout.Checked = uiHidden.SetupAbout;
            CheckHiddenSetupFeedback.Checked = uiHidden.SetupFeedback;
            CheckHiddenSetupLog.Checked = uiHidden.SetupLog;

            // 子页面 工具
            CheckHiddenToolsGameLink.Checked = uiHidden.ToolsGameLink;
            CheckHiddenToolsTest.Checked = uiHidden.ToolsTest;

            // 子页面 实例设置
            CheckHiddenVersionEdit.Checked = uiHidden.InstanceEdit;
            CheckHiddenVersionExport.Checked = uiHidden.InstanceExport;
            CheckHiddenVersionSave.Checked = uiHidden.InstanceSave;
            CheckHiddenVersionScreenshot.Checked = uiHidden.InstanceScreenshot;
            CheckHiddenVersionMod.Checked = uiHidden.InstanceMod;
            CheckHiddenVersionResourcePack.Checked = uiHidden.InstanceResourcePack;
            CheckHiddenVersionShader.Checked = uiHidden.InstanceShader;
            CheckHiddenVersionSchematic.Checked = uiHidden.InstanceSchematic;
            CheckHiddenVersionServer.Checked = uiHidden.InstanceServer;

            // 特定功能
            CheckHiddenFunctionSelect.Checked = uiHidden.FunctionSelect;
            CheckHiddenFunctionModUpdate.Checked = uiHidden.FunctionModUpdate;
            CheckHiddenFunctionHidden.Checked = uiHidden.FunctionHidden;
        }
        catch (NullReferenceException ex)
        {
            Common.Log(
                ex,
                Lang.Text("Setup.Ui.Error.ConfigReset"),
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Ui.Error.ConfigReset"));
            Reset();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Setup.Ui.Error.LoadFailed"),
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Ui.Error.LoadFailed"));
        }
    }

    // 初始化
    public void Reset()
    {
        try
        {
            Config.Preference.Reset();
            Common.Log("[Setup] 已初始化个性化设置！");
            HintService.Show(Hints.Success(Lang.Text("Setup.Ui.Initialized"), log: false));
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Setup.Ui.Error.InitFailed"),
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Ui.Error.InitFailed"));
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void SliderChange(object senderRaw, bool user)
    {
        var sender = (MySlider)senderRaw;
        if (!Animations.AniExitsAnyControl())
            SetByTag(sender.Tag?.ToString(), sender.Value);
    }

    private void ComboChange(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        if (Animations.AniExitsAnyControl() || sender.SelectedIndex < 0)
            return;
        SetByTag(sender.Tag?.ToString(), sender.SelectedIndex);
    }

    private void CheckBoxChange(object senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw;
        if (!Animations.AniExitsAnyControl())
            SetByTag(sender.Tag?.ToString(), sender.Checked);
    }

    private void TextBoxChange(object senderRaw, RoutedEventArgs e)
    {
        var sender = (MyTextBox)senderRaw;
        if (!Animations.AniExitsAnyControl())
            SetByTag(sender.Tag?.ToString(), sender.Text);
    }

    private void RadioBoxChange(object senderRaw, Common.RouteEventArgs e)
    {
        var sender = (MyRadioBox)senderRaw;
        var gotCfg = sender.Tag?.ToString()?.Split("/") ?? Array.Empty<string>();
        if (!Animations.AniExitsAnyControl() && gotCfg.Length >= 2)
            SetByTag(gotCfg[0], int.Parse(gotCfg[1]));
    }

    private static void SetByTag(string tag, object value)
    {
        ConfigService.TrySetValue(tag, value);
    }

    private void ComboFontChange(object sender, SelectionChangedEventArgs e)
    {
        if (!Animations.AniExitsAnyControl()) Config.Preference.Font = ComboUiFont.SelectedFontTag;
    }

    private void ComboMotdFontChange(object sender, SelectionChangedEventArgs e)
    {
        if (!Animations.AniExitsAnyControl()) Config.Preference.MotdFont = ComboUiMotdFont.SelectedFontTag;
    }

    // 背景图片
    private void BtnUIBgOpen_Click(object sender, MouseButtonEventArgs e)
    {
        Common.OpenExplorer(Common.ExePath + @"PCL\Pictures\");
    }

    private void BtnBackgroundRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        BackgroundRefresh(true, true);
    }

    public void BackgroundRefreshUI(bool show, int count)
    {
        if (PanBackgroundOpacity is null)
            return;
        _vm.ApplyBackgroundState(show, count);
        LocalizedText.TryBind(
            CardBackground,
            "Title",
            () => show
                ? Lang.Text("Setup.Ui.Background.TitleWithCount", count)
                : Lang.Text("Setup.Ui.Background.TitleDefault"));
        CardBackground.TriggerForceResize();
    }

    private async void BtnBackgroundClear_Click(object sender, MouseButtonEventArgs e)
    {
        _vm.BackgroundClearEnabled = false;
        try
        {
            if (!await _vm.ConfirmBackgroundClearAsync())
                return;
            Common.DeleteDirectory(Common.ExePath + @"PCL\Pictures");
            BackgroundRefresh(false, true);
            HintService.Show(Hints.Success(Lang.Text("Setup.Ui.Background.Clear.Success")));
        }
        catch (Exception ex)
        {
            Common.Log(ex, "清空背景内容失败", Common.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
        finally
        {
            _vm.BackgroundClearEnabled = true;
        }
    }

    /// <summary>
    /// 刷新背景图片及设置页 UI。
    /// </summary>
    /// <param name="isHint">是否显示刷新提示。</param>
    /// <param name="refresh">是否刷新图片显示。</param>
    public static void BackgroundRefresh(bool isHint, bool refresh)
    {
        try
        {
            // 获取可用的图片文件
            Directory.CreateDirectory(Common.ExePath + @"PCL\Pictures\");
            var pic = Common.EnumerateFiles(Common.ExePath + @"PCL\Pictures\").Where(file =>
                    !(file.Extension.Equals(".ini", StringComparison.OrdinalIgnoreCase) ||
                      file.Extension.Equals(".db", StringComparison.OrdinalIgnoreCase))).Select(file => file.FullName)
                .ToList();

            // 视频加载异常处理

            EventHandler<ExceptionRoutedEventArgs> videoHandler = (sender, e) =>
            {
                var videoEx = e.ErrorException;
                var videoAddress = Launcher.frmMain.VideoBack.Source.ToString();
                if (Launcher.frmMain.VideoBack.Source is not null)
                {
                    VideoBackground.VideoStop();

                    if (videoEx.Message.Contains("0xC00D109B"))
                        Common.Log(
                            $"""
                             刷新背景内容失败，该视频文件可能并非 H.264（AVC）格式。
                             你可以尝试使用视频转码工具打开视频文件并设定目标格式为 H.264（AVC），然后转码该视频。
                             文件：{videoAddress}
                             """,
                            Common.LogLevel.Msgbox,
                            userSummary: Lang.Text("Setup.Ui.Error.BackgroundVideoUnsupported"));
                    else
                        Common.Log(
                            videoEx,
                            $"刷新背景内容失败（{videoAddress}）",
                            Common.LogLevel.Msgbox,
                            userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
                }
            };
            Launcher.frmMain.VideoBack.MediaFailed -= videoHandler;
            VideoBackground.GamingStateChanged -= VideoBackground.OnGamingStateChanged;
            VideoBackground.ForcePlayChanged -= VideoBackground.OnForcePlayChanged;
            VideoBackground.GamingStateChanged += VideoBackground.OnGamingStateChanged;
            VideoBackground.ForcePlayChanged += VideoBackground.OnForcePlayChanged;
            if (!Config.Preference.Background.AutoPauseVideo)
                VideoBackground.ForcePlay = true;
            // 加载
            if (pic.Count == 0)
            {
                if (refresh)
                {
                    if (Launcher.frmMain.ImgBack.Visibility == Visibility.Collapsed)
                    {
                        if (isHint)
                            HintService.Show(Hints.Error(Lang.Text("Setup.Ui.Background.NoAvailableContent")));
                    }
                    else
                    {
                        Launcher.frmMain.ImgBack.Visibility = Visibility.Collapsed;
                        if (isHint)
                            HintService.Show(Hints.Success(Lang.Text("Setup.Ui.Background.Cleared")));
                    }
                }

                if (Launcher.frmSetupUi is not null)
                    Launcher.frmSetupUi.BackgroundRefreshUI(false, 0);
            }
            else
            {
                if (refresh)
                {
                    var address = RandomUtils.PickRandom(pic);
                    try
                    {
                        Launcher.frmMain.ImgBack.Background = null;
                        VideoBackground.VideoStop();
                        Common.Log("[UI] 加载背景内容：" + address);
                        Launcher.frmMain.ImgBack.Background = new MyBitmap(address);
                        _ = Config.Preference.Background.WallpaperSuitMode;
                        Launcher.frmMain.ImgBack.Visibility = Visibility.Visible;
                        if (isHint)
                                HintService.Show(Hints.Success(Lang.Text("Setup.Ui.Background.Refresh.Success", Common.GetFileNameFromPath(address)), log: false));
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            Launcher.frmMain.VideoBack.MediaFailed += videoHandler;
                            Common.Log(ex, "[UI] 加载背景图片失败" + address);
                            if (Common.modeDebug)
                                HintService.Show(Hints.Info(Lang.Text("Setup.Ui.Background.ImageLoadFailed", address)));
                            Launcher.frmMain.ImgBack.Visibility = Visibility.Visible;
                            Launcher.frmMain.VideoBack.Source = new Uri(address, UriKind.Absolute);
                            VideoBackground.VideoPlay();
                            if (isHint)
                            HintService.Show(Hints.Success(Lang.Text("Setup.Ui.Background.Refresh.Success", Common.GetFileNameFromPath(address)), log: false));
                        }
                        catch (Exception playEx)
                        {
                            Common.Log(playEx, "播放背景内容时出现未知错误：");
                        }
                    }
                }

                if (Launcher.frmSetupUi is not null)
                    Launcher.frmSetupUi.BackgroundRefreshUI(true, pic.Count);
            }
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                "刷新背景内容时出现未知错误",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    // 顶部栏
    private void BtnLogoChange_Click(object sender, MouseButtonEventArgs e)
    {
        var fileName = SystemDialogs.SelectFile(
            Lang.Text("Setup.Ui.ImageFile.Filter"),
            Lang.Text("Setup.Ui.ImageFile.SelectTitle"));
        if (string.IsNullOrEmpty(fileName))
            return;
        try
        {
            // 拷贝文件
            File.Delete(Common.ExePath + @"PCL\Logo.png");
            Common.CopyFile(fileName, Common.ExePath + @"PCL\Logo.png");
            // 设置当前显示
            Launcher.frmMain.ImageTitleLogo.Source = null; // 防止因为 Source 属性前后的值相同而不更新 (#5628)
            Launcher.frmMain.ImageTitleLogo.Source = Common.ExePath + @"PCL\Logo.png";
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("参数无效"))
                Common.Log(
                    """
                    改变标题栏图片失败，该图片文件可能并非标准格式。
                    你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。
                    """,
                    Common.LogLevel.Msgbox,
                    userSummary: Lang.Text("Setup.Ui.Error.TitleImageInvalidFormat"));
            else
                Common.Log(
                    ex,
                    "设置标题栏图片失败",
                    Common.LogLevel.Msgbox,
                    userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
            Launcher.frmMain.ImageTitleLogo.Source = null;
        }
    }

    private void RadioLogoType3_Check(object sender, Common.RouteEventArgs e)
    {
        if (!(!Animations.AniExitsAnyControl() && e.raiseByMouse))
            return;
        Refresh: ;

        // 已有图片则不再选择
        if (File.Exists(Common.ExePath + @"PCL\Logo.png"))
        {
            try
            {
                Launcher.frmMain.ImageTitleLogo.Source = null; // 防止因为 Source 属性前后的值相同而不更新 (#5628)
                Launcher.frmMain.ImageTitleLogo.Source = Common.ExePath + @"PCL\Logo.png";
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("参数无效"))
                    Common.Log(
                        """
                        调整标题栏图片失败，该图片文件可能并非标准格式。
                        你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。
                        """,
                        Common.LogLevel.Msgbox,
                        userSummary: Lang.Text("Setup.Ui.Error.TitleImageResizeInvalidFormat"));
                else
                    Common.Log(
                        ex,
                        "调整标题栏图片失败",
                        Common.LogLevel.Msgbox,
                        userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
                Launcher.frmMain.ImageTitleLogo.Source = null;
                e.handled = true;
                try
                {
                    File.Delete(Common.ExePath + @"PCL\Logo.png");
                }
                catch (Exception exx)
                {
                    Common.Log(
                        exx,
                        "清理错误的标题栏图片失败",
                        Common.LogLevel.Msgbox,
                        userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
                }
            }

            return;
        }

        // 没有图片则要求选择
        var fileName = SystemDialogs.SelectFile(Lang.Text("Setup.Ui.ImageFile.Filter"), Lang.Text("Setup.Ui.ImageFile.SelectTitle"));
        if (string.IsNullOrEmpty(fileName))
        {
            Launcher.frmMain.ImageTitleLogo.Source = null;
            e.handled = true;
        }
        else
        {
            try
            {
                // 拷贝文件
                File.Delete(Common.ExePath + @"PCL\Logo.png");
                Common.CopyFile(fileName, Common.ExePath + @"PCL\Logo.png");
                goto Refresh;
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    "复制标题栏图片失败",
                    Common.LogLevel.Msgbox,
                    userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
            }
        }
    }

    private void BtnLogoDelete_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            File.Delete(Common.ExePath + @"PCL\Logo.png");
            RadioLogoType1.SetChecked(true, true);
            HintService.Show(Hints.Success(Lang.Text("Setup.Ui.Logo.Clear.Success")));
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "清空标题栏图片失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    // 背景音乐
    private void BtnMusicOpen_Click(object sender, MouseButtonEventArgs e)
    {
        Common.OpenExplorer(Common.ExePath + @"PCL\Musics\");
    }

    private void BtnMusicRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        Music.MusicRefreshPlay(true);
    }

    public void MusicRefreshUI()
    {
        if (PanBackgroundOpacity is null)
            return;
        var hasMusic = Music.musicAllList.Any();
        var musicCount = Music.musicAllList.Count;
        _vm.ApplyMusicState(hasMusic, hasMusic ? Common.EnumerateFiles(Common.ExePath + @"PCL\Musics\").Count() : 0);
        LocalizedText.TryBind(
            CardMusic,
            "Title",
            () => hasMusic
                ? Lang.Text("Setup.Ui.Music.TitleWithCount", musicCount)
                : Lang.Text("Setup.Ui.Music.Title"));
        CardMusic.TriggerForceResize();
    }

    private async void BtnMusicClear_Click(object sender, MouseButtonEventArgs e)
    {
        _vm.MusicClearEnabled = false;
        try
        {
            if (!await _vm.ConfirmMusicClearAsync())
                return;
            Common.RunInThread(() =>
            {
                HintService.Show(Hints.Info(Lang.Text("Setup.Ui.Music.Deleting")));
                // 停止播放音乐
                Music.musicNAudio = null;
                Music.musicWaitingList = new List<string>();
                Music.musicAllList = new List<string>();
                Thread.Sleep(200);
                // 删除文件
                try
                {
                    Common.DeleteDirectory(Common.ExePath + @"PCL\Musics");
                    // DisableSMTCSupport()
                    HintService.Show(Hints.Success(Lang.Text("Setup.Ui.Music.Delete.Success")));
                }
                catch (Exception ex)
                {
                    Common.Log(
                        ex,
                        "删除背景音乐失败",
                        Common.LogLevel.Msgbox,
                        userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
                }

                try
                {
                    Directory.CreateDirectory(Common.ExePath + @"PCL\Musics");
                    Common.RunInUi(() => Music.MusicRefreshPlay(false));
                }
                catch (Exception ex)
                {
                    Common.Log(
                        ex,
                        "重建背景音乐文件夹失败",
                        Common.LogLevel.Msgbox,
                        userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
                }
            });
        }
        catch (Exception ex)
        {
            Common.Log(ex, "清空背景音乐失败", Common.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
        finally
        {
            _vm.MusicClearEnabled = true;
        }
    }

    private void CheckMusicStart_Change(object sender, bool user)
    {
        CheckBoxChange(sender, user);
        if (Animations.AniExitsAnyControl())
            return;
        if (CheckMusicStart.Checked == true)
            CheckMusicStop.Checked = false;
    }

    private void CheckMusicStop_Change()
    {
        if (Animations.AniExitsAnyControl())
            return;
        if (CheckMusicStop.Checked == true)
            CheckMusicStart.Checked = false;
    }

    // 主页

    private void BtnCustomRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        Launcher.frmLaunchRight.ForceRefresh();
        HintService.Show(Hints.Success(Lang.Text("Setup.Ui.Homepage.Refresh.Success")));
    }

    private void BtnCustomTutorial_Click(object sender, MouseButtonEventArgs e)
    {
        Common.OpenWebsite("https://docs.pclc.cc/ce/customization/xaml-format");
    }

    // 主题
    private void ThemeColor_Change(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        if (Animations.AniExitsAnyControl() || sender.SelectedIndex < 0)
            return;
        SetByTag(sender.Tag?.ToString(), sender.SelectedIndex);
        ThemeManager.ThemeRefresh();
    }

    // 赞助
    private void BtnLauncherDonate_Click(object sender, MouseButtonEventArgs e)
    {
        Common.OpenWebsite("https://afdian.com/a/LTCat");
    }

    // 滑动条
    private void SliderLoad()
    {
        SliderMusicVolume.getHintText = new Func<object, object>(PageSetupUIViewModel.FormatMusicVolumeHint);
        SliderLauncherOpacity.getHintText = new Func<object, object>(PageSetupUIViewModel.FormatLauncherOpacityHint);
        SliderBackgroundOpacity.getHintText = new Func<object, object>(PageSetupUIViewModel.FormatBackgroundOpacityHint);
        SliderBackgroundBlur.getHintText = new Func<object, object>(PageSetupUIViewModel.FormatPixelHint);
        SliderBlurValue.getHintText = new Func<object, object>(PageSetupUIViewModel.FormatPixelHint);
        SliderBlurSamplingRate.getHintText = new Func<object, object>(PageSetupUIViewModel.FormatSamplingRateHint);
    }

    private void CheckMusicStart_OnChange(object sender, bool user)
    {
        CheckBoxChange(sender, user);
        CheckMusicStart_Change(sender, user);
    }

    private void CheckMusicStop_OnChange(object sender, bool user)
    {
        CheckBoxChange(sender, user);
        CheckMusicStop_Change();
    }

    #region 功能隐藏

    /// <summary>
    /// 是否强制显示被禁用的功能。
    /// </summary>
    public static bool HiddenForceShow
    {
        get => field;
        set
        {
            field = value;
            HiddenRefresh();
        }
    }

    /// <summary>
    /// 更新功能隐藏带来的显示变化。
    /// </summary>
    public static void HiddenRefresh()
    {
        if (Launcher.frmMain.PanTitleSelect is null || !Launcher.frmMain.PanTitleSelect.IsLoaded)
            return;
        try
        {
            // 获取配置组引用以缩短代码
            var conf = Config.Preference.Hide;

            // 顶部栏：下载、设置、工具
            var isAllTitleHidden = !HiddenForceShow && conf.PageDownload && conf.PageSetup && conf.PageTools;

            if (isAllTitleHidden)
            {
                Launcher.frmMain.PanTitleSelect.Visibility = Visibility.Collapsed;
            }
            else
            {
                Launcher.frmMain.PanTitleSelect.Visibility = Visibility.Visible;
                Launcher.frmMain.BtnTitleSelect1.Visibility = !HiddenForceShow && conf.PageDownload
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                Launcher.frmMain.BtnTitleSelect2.Visibility =
                    !HiddenForceShow && conf.PageSetup ? Visibility.Collapsed : Visibility.Visible;
                Launcher.frmMain.BtnTitleSelect3.Visibility =
                    !HiddenForceShow && conf.PageTools ? Visibility.Collapsed : Visibility.Visible;
            }

            // 功能隐藏设置卡片
            if (Launcher.frmSetupUi is not null)
            {
                Launcher.frmSetupUi.ViewModel.ApplyFeatureHideState(HiddenForceShow, conf.FunctionHidden);
                LocalizedText.TryBind(
                    Launcher.frmSetupUi.CardSwitch,
                    "Title",
                    () => HiddenForceShow
                        ? Lang.Text("Setup.Ui.FeatureHide.TitleTemporarilyDisabled")
                        : Lang.Text("Setup.Ui.FeatureHide.Title"));
            }

            // 设置子页面 (FrmSetupLeft)
            if (Launcher.frmSetupLeft is not null)
            {
                Launcher.frmSetupLeft.ItemLaunch.Visibility =
                    !HiddenForceShow && conf.SetupLaunch ? Visibility.Collapsed : Visibility.Visible;
                Launcher.frmSetupLeft.ItemUI.Visibility =
                    !HiddenForceShow && conf.SetupUi ? Visibility.Collapsed : Visibility.Visible;
                Launcher.frmSetupLeft.ItemLauncherLanguage.Visibility = !HiddenForceShow && conf.SetupLauncherLanguage
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                Launcher.frmSetupLeft.ItemGameManage.Visibility = !HiddenForceShow && conf.SetupGameManage
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                Launcher.frmSetupLeft.ItemLauncherMisc.Visibility = !HiddenForceShow && conf.SetupLauncherMisc
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                Launcher.frmSetupLeft.ItemJava.Visibility =
                    !HiddenForceShow && conf.SetupJava ? Visibility.Collapsed : Visibility.Visible;
                Launcher.frmSetupLeft.ItemUpdate.Visibility =
                    !HiddenForceShow && conf.SetupUpdate ? Visibility.Collapsed : Visibility.Visible;
                Launcher.frmSetupLeft.ItemGameLink.Visibility = !HiddenForceShow && conf.SetupGameLink
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                Launcher.frmSetupLeft.ItemAbout.Visibility =
                    !HiddenForceShow && conf.SetupAbout ? Visibility.Collapsed : Visibility.Visible;
                Launcher.frmSetupLeft.ItemFeedback.Visibility = !HiddenForceShow && conf.SetupFeedback
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                Launcher.frmSetupLeft.ItemLog.Visibility =
                    !HiddenForceShow && conf.SetupLog ? Visibility.Collapsed : Visibility.Visible;

                var categories = new[]
                {
                    (Launcher.frmSetupLeft.TextGameCategory,
                        !(conf.SetupLaunch && conf.SetupJava && conf.SetupGameManage)),
                    (Launcher.frmSetupLeft.TextToolsCategory, !conf.SetupGameLink),
                    (Launcher.frmSetupLeft.TextLauncherCategory, !(conf.SetupUi && conf.SetupLauncherLanguage && conf.SetupLauncherMisc)),
                    (Launcher.frmSetupLeft.TextAboutCategory,
                        !(conf.SetupAbout && conf.SetupUpdate && conf.SetupFeedback && conf.SetupLog))
                };

                foreach (var category in categories)
                {
                    var isVisible = category.Item2 || HiddenForceShow;
                    category.Item1.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                    if (isVisible)
                        category.Item1.Opacity = 0.6d;
                }

                // 统计设置页可用项数量
                var setupCount = 0;
                if (!conf.SetupLaunch)
                    setupCount += 1;
                if (!conf.SetupUi)
                    setupCount += 1;
                if (!conf.SetupLauncherLanguage)
                    setupCount += 1;
                if (!conf.SetupGameManage)
                    setupCount += 1;
                if (!conf.SetupLauncherMisc)
                    setupCount += 1;
                if (!conf.SetupJava)
                    setupCount += 1;
                if (!conf.SetupUpdate)
                    setupCount += 1;
                if (!conf.SetupGameLink)
                    setupCount += 1;
                if (!conf.SetupAbout)
                    setupCount += 1;
                if (!conf.SetupFeedback)
                    setupCount += 1;
                if (!conf.SetupLog)
                    setupCount += 1;
                Launcher.frmSetupLeft.PanItem.Visibility =
                    setupCount < 2 && !HiddenForceShow ? Visibility.Collapsed : Visibility.Visible;
            }

            // 工具子页面 (FrmToolsLeft)
            if (Launcher.frmToolsLeft is not null)
            {
                Launcher.frmToolsLeft.ItemGameLink.Visibility = !HiddenForceShow && conf.ToolsGameLink
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                Launcher.frmToolsLeft.ItemTest.Visibility =
                    !HiddenForceShow && conf.ToolsTest ? Visibility.Collapsed : Visibility.Visible;

                // 处理分类标题
                var isGameLinkVisible = (!HiddenForceShow && !conf.ToolsGameLink) || HiddenForceShow;
                Launcher.frmToolsLeft.TextGameLinkCategory.Visibility = isGameLinkVisible ? Visibility.Visible : Visibility.Collapsed;
                if (isGameLinkVisible) Launcher.frmToolsLeft.TextGameLinkCategory.Opacity = 0.6;

                var isToolsVisible = (!HiddenForceShow && !conf.ToolsTest) || HiddenForceShow;
                Launcher.frmToolsLeft.TextToolsCategory.Visibility = isToolsVisible ? Visibility.Visible : Visibility.Collapsed;
                if (isToolsVisible) Launcher.frmToolsLeft.TextToolsCategory.Opacity = 0.6;

                // 统计工具页可用项数量
                var toolsCount = 0;
                if (!conf.ToolsGameLink)
                    toolsCount += 1;
                if (!conf.ToolsTest)
                    toolsCount += 1;
                Launcher.frmToolsLeft.PanItem.Visibility =
                    toolsCount < 2 && !HiddenForceShow ? Visibility.Collapsed : Visibility.Visible;
            }

            // 其他入口刷新
            if (Launcher.frmMain.pageCurrent == FormMain.PageType.InstanceSelect)
                Launcher.frmSelectRight.BtnEmptyDownload_Loaded();
            if (Launcher.frmMain.pageCurrent == FormMain.PageType.Launch)
                Launcher.frmLaunchLeft.RefreshButtonsUI();
            if (Launcher.frmMain.pageCurrent == FormMain.PageType.InstanceSetup &&
                Launcher.frmInstanceModDisabled is not null)
                Launcher.frmInstanceModDisabled.BtnDownload_Loaded();
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                "刷新功能隐藏项目失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    // ================= 设置页面协同 =================
    private void HiddenSetupMain()
    {
        var isChecked = (bool)CheckHiddenPageSetup.Checked;
        CheckHiddenSetupLaunch.Checked = isChecked;
        CheckHiddenSetupUI.Checked = isChecked;
        CheckHiddenSetupLauncherLanguage.Checked = isChecked;
        CheckHiddenSetupGameManage.Checked = isChecked;
        CheckHiddenLauncherMisc.Checked = isChecked;
        CheckHiddenSetupJava.Checked = isChecked;
        CheckHiddenSetupUpdate.Checked = isChecked;
        CheckHiddenSetupGameLink.Checked = isChecked;
        CheckHiddenSetupAbout.Checked = isChecked;
        CheckHiddenSetupFeedback.Checked = isChecked;
        CheckHiddenSetupLog.Checked = isChecked;
    }

    // ================= 设置页面协同 =================
    private void HiddenSetupMain(object sender, bool user)
    {
        if (!user)
            return; // 仅处理用户点击，防止死循环
        var isChecked = (bool)CheckHiddenPageSetup.Checked;
        CheckHiddenSetupLaunch.Checked = isChecked;
        CheckHiddenSetupUI.Checked = isChecked;
        CheckHiddenSetupLauncherLanguage.Checked = isChecked;
        CheckHiddenSetupGameManage.Checked = isChecked;
        CheckHiddenLauncherMisc.Checked = isChecked;
        CheckHiddenSetupJava.Checked = isChecked;
        CheckHiddenSetupUpdate.Checked = isChecked;
        CheckHiddenSetupGameLink.Checked = isChecked;
        CheckHiddenSetupAbout.Checked = isChecked;
        CheckHiddenSetupFeedback.Checked = isChecked;
        CheckHiddenSetupLog.Checked = isChecked;
    }

    private void HiddenSetupSub(object sender, bool user)
    {
        if (!user)
            return;
        var conf = Config.Preference.Hide;
        // 判断是否全部勾选
        var allChecked = conf.SetupLaunch && conf.SetupUi && conf.SetupLauncherLanguage && conf.SetupJava &&
                         conf.SetupUpdate && conf.SetupGameLink && conf.SetupAbout && conf.SetupFeedback &&
                         conf.SetupLog && conf.SetupLauncherMisc && conf.SetupGameManage;
        CheckHiddenPageSetup.Checked = allChecked;
    }

    // ================= 工具页面协同 =================
    private void HiddenToolsMain(object sender, bool user)
    {
        if (!user)
            return;
        var isChecked = (bool)CheckHiddenPageTools.Checked;
        CheckHiddenToolsGameLink.Checked = isChecked;
        CheckHiddenToolsTest.Checked = isChecked;
    }

    private void HiddenToolsSub(object sender, bool user)
    {
        if (!user)
            return;
        var conf = Config.Preference.Hide;
        var allChecked = conf.ToolsGameLink && conf.ToolsTest;
        CheckHiddenPageTools.Checked = allChecked;
    }

    // 警告提示
    private void HiddenHint(object sender, bool user)
    {
        if (!Animations.AniExitsAnyControl() && sender is MyCheckBox checkBox && checkBox.Checked == true)
            HintService.Show(Hints.Info(Lang.Text("Setup.Ui.FeatureHide.TemporaryHint")));
    }

    #endregion
}
