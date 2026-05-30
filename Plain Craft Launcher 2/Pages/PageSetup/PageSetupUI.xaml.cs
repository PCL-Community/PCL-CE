using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupUI
{
    public string[] ThemeColors => Basics.IsAprilFool 
        ? [Lang.Text("Setup.Ui.ThemeColor.SkyBlue"), Lang.Text("Setup.Ui.ThemeColor.CatBlue"), Lang.Text("Setup.Ui.ThemeColor.CrashBlue"), Lang.Text("Setup.Ui.ThemeColor.Hmcl")]
        : [Lang.Text("Setup.Ui.ThemeColor.SkyBlue"), Lang.Text("Setup.Ui.ThemeColor.CatBlue"), Lang.Text("Setup.Ui.ThemeColor.CrashBlue")];
    
    public new bool isLoaded;

    public PageSetupUI()
    {
        InitializeComponent();
        Loaded += PageSetupUI_Loaded;
        Loaded += (_, _) => HiddenRefresh();
    }

    private void PageSetupUI_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        ModAnimation.AniControlEnabled += 1;
        Reload(); // #4826，在每次进入页面时都刷新一下
        ModAnimation.AniControlEnabled -= 1;

        // 非重复加载部分
        if (isLoaded)
            return;
        isLoaded = true;

        SliderLoad();

        PanLauncherHide.Visibility = Visibility.Visible;
    }

    public void Reload()
    {
        try
        {
            // 启动器
            SliderLauncherOpacity.Value = Config.Preference.Theme.WindowOpacity;
            CheckLauncherLogo.Checked = Config.Preference.ShowStartupLogo;
            ComboDarkMode.SelectedIndex = (int)Config.Preference.Theme.ColorMode;
            ComboDarkColor.SelectedIndex = (int)Config.Preference.Theme.DarkColor;
            ComboLightColor.SelectedIndex = (int)Config.Preference.Theme.LightColor;
            CheckShowLaunchingHint.Checked = Config.Preference.ShowLaunchingHint;

            // 字体设置
            ComboUiFont.SelectedFontTag = Config.Preference.Font;
            ComboUiMotdFont.SelectedFontTag = Config.Preference.MotdFont;

            CheckBlur.Checked = Config.Preference.Blur.IsEnabled;
            SliderBlurValue.Value = Config.Preference.Blur.Radius;
            SliderBlurSamplingRate.Value = Config.Preference.Blur.SamplingRate;
            ComboBlurType.SelectedIndex = Config.Preference.Blur.KernelType;
            PanBlurValue.Visibility = CheckBlur.Checked == true ? Visibility.Visible : Visibility.Collapsed;
            CheckLockWindowSize.Checked = Config.Preference.LockWindowSize;

            // 背景图片
            SliderBackgroundOpacity.Value = Config.Preference.Background.WallpaperOpacity;
            SliderBackgroundBlur.Value = Config.Preference.Background.WallpaperBlurRadius;
            ComboBackgroundSuit.SelectedIndex = Config.Preference.Background.WallpaperSuitMode;
            CheckBackgroundColorful.Checked = Config.Preference.Background.BackgroundColorful;
            var autoPauseVideo = Config.Preference.Background.AutoPauseVideo;
            CheckAutoPauseVideo.Checked = autoPauseVideo;
            if (ModVideoBack.IsGaming)
                if (autoPauseVideo)
                    BtnBackgroundRefresh.IsEnabled = false;

            BackgroundRefresh(false, false);

            // 标题栏
            ((MyRadioBox)FindName("RadioLogoType" + (int)Config.Preference.WindowTitleType))
                .Checked = true;
            CheckLogoLeft.Visibility = RadioLogoType0.Checked ? Visibility.Visible : Visibility.Collapsed;
            PanLogoText.Visibility = RadioLogoType2.Checked ? Visibility.Visible : Visibility.Collapsed;
            PanLogoChange.Visibility = RadioLogoType3.Checked ? Visibility.Visible : Visibility.Collapsed;
            TextLogoText.Text = Config.Preference.WindowTitleCustomText;
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
                ComboCustomPreset.SelectedIndex = Config.Preference.Homepage.SelectedPreset;
            }
            catch
            {
                Config.Preference.Homepage.SelectedPresetConfig.Reset();
            }

            ((MyRadioBox)FindName("RadioCustomType" + Config.Preference.Homepage.Type)).Checked = true;
            TextCustomNet.Text = Config.Preference.Homepage.CustomUrl;
            ModSetup.UiCustomType(Config.Preference.Homepage.Type);

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
            CheckHiddenToolsHelp.Checked = uiHidden.ToolsHelp;
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
            ModBase.Log(ex, Lang.Text("Setup.Ui.Error.ConfigReset"), ModBase.LogLevel.Msgbox);
            Reset();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Setup.Ui.Error.LoadFailed"), ModBase.LogLevel.Feedback);
        }
    }

    // 初始化
    public void Reset()
    {
        try
        {
            Config.Preference.Reset();
            ModBase.Log("[Setup] 已初始化个性化设置！");
            ModMain.Hint(Lang.Text("Setup.Ui.Initialized"), ModMain.HintType.Finish, false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Setup.Ui.Error.InitFailed"), ModBase.LogLevel.Msgbox);
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void SliderChange(object senderRaw, bool user)
    {
        var sender = (MySlider)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.Value);
    }

    private void ComboChange(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.SelectedIndex);
    }

    private void CheckBoxChange(object senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.Checked);
    }

    private void TextBoxChange(object senderRaw, RoutedEventArgs e)
    {
        var sender = (MyTextBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.Text);
    }

    private void RadioBoxChange(object senderRaw, ModBase.RouteEventArgs e)
    {
        var sender = (MyRadioBox)senderRaw;
        var gotCfg = sender.Tag?.ToString()?.Split("/") ?? Array.Empty<string>();
        if (ModAnimation.AniControlEnabled == 0 && gotCfg.Length >= 2)
            SetByTag(gotCfg[0], int.Parse(gotCfg[1]));
    }

    private static void SetByTag(string tag, object value)
    {
        switch (tag)
        {
            case "UiLauncherTransparent": Config.Preference.Theme.WindowOpacity = (int)value; break;
            case "UiBackgroundOpacity": Config.Preference.Background.WallpaperOpacity = (int)value; break;
            case "UiBackgroundBlur": Config.Preference.Background.WallpaperBlurRadius = (int)value; break;
            case "UiBlurValue": Config.Preference.Blur.Radius = (int)value; break;
            case "UiBlurSamplingRate": Config.Preference.Blur.SamplingRate = (int)value; break;
            case "UiMusicVolume": Config.Preference.Music.Volume = (int)value; break;

            case "UiLauncherLogo": Config.Preference.ShowStartupLogo = (bool)value; break;
            case "UiShowLaunchingHint": Config.Preference.ShowLaunchingHint = (bool)value; break;
            case "UiLockWindowSize": Config.Preference.LockWindowSize = (bool)value; break;
            case "UiBlur": Config.Preference.Blur.IsEnabled = (bool)value; break;
            case "UiAutoPauseVideo": Config.Preference.Background.AutoPauseVideo = (bool)value; break;
            case "UiBackgroundColorful": Config.Preference.Background.BackgroundColorful = (bool)value; break;
            case "UiMusicRandom": Config.Preference.Music.ShufflePlayback = (bool)value; break;
            case "UiMusicAuto": Config.Preference.Music.StartOnStartup = (bool)value; break;
            case "UiMusicStart": Config.Preference.Music.StartInGame = (bool)value; break;
            case "UiMusicStop": Config.Preference.Music.StopInGame = (bool)value; break;
            case "UiMusicSMTC": Config.Preference.Music.EnableSMTC = (bool)value; break;
            case "UiLogoLeft": Config.Preference.TopBarLeftAlign = (bool)value; break;

            case "UiDarkMode": Config.Preference.Theme.ColorMode = (ColorMode)(int)value; break;
            case "UiDarkColor": Config.Preference.Theme.DarkColor = (ColorTheme)(int)value; break;
            case "UiLightColor": Config.Preference.Theme.LightColor = (ColorTheme)(int)value; break;
            case "UiBlurType": Config.Preference.Blur.KernelType = (int)value; break;
            case "UiBackgroundSuit": Config.Preference.Background.WallpaperSuitMode = (int)value; break;
            case "UiCustomPreset": Config.Preference.Homepage.SelectedPreset = (int)value; break;
            case "UiLogoType": Config.Preference.WindowTitleType = (LauncherTitleType)(int)value; break;
            case "UiLogoText": Config.Preference.WindowTitleCustomText = (string)value; break;
            case "UiCustomType": Config.Preference.Homepage.Type = (int)value; break;

            case "UiHiddenPageDownload": Config.Preference.Hide.PageDownload = (bool)value; break;
            case "UiHiddenPageSetup": Config.Preference.Hide.PageSetup = (bool)value; break;
            case "UiHiddenPageTools": Config.Preference.Hide.PageTools = (bool)value; break;
            case "UiHiddenSetupLaunch": Config.Preference.Hide.SetupLaunch = (bool)value; break;
            case "UiHiddenSetupUi": Config.Preference.Hide.SetupUi = (bool)value; break;
            case "UiHiddenSetupLauncherLanguage": Config.Preference.Hide.SetupLauncherLanguage = (bool)value; break;
            case "UiHiddenSetupLauncherMisc": Config.Preference.Hide.SetupLauncherMisc = (bool)value; break;
            case "UiHiddenSetupGameManage": Config.Preference.Hide.SetupGameManage = (bool)value; break;
            case "UiHiddenSetupJava": Config.Preference.Hide.SetupJava = (bool)value; break;
            case "UiHiddenSetupUpdate": Config.Preference.Hide.SetupUpdate = (bool)value; break;
            case "UiHiddenSetupGameLink": Config.Preference.Hide.SetupGameLink = (bool)value; break;
            case "UiHiddenSetupAbout": Config.Preference.Hide.SetupAbout = (bool)value; break;
            case "UiHiddenSetupFeedback": Config.Preference.Hide.SetupFeedback = (bool)value; break;
            case "UiHiddenSetupLog": Config.Preference.Hide.SetupLog = (bool)value; break;
            case "UiHiddenToolsGameLink": Config.Preference.Hide.ToolsGameLink = (bool)value; break;
            case "UiHiddenToolsHelp": Config.Preference.Hide.ToolsHelp = (bool)value; break;
            case "UiHiddenToolsTest": Config.Preference.Hide.ToolsTest = (bool)value; break;
            case "UiHiddenVersionEdit": Config.Preference.Hide.InstanceEdit = (bool)value; break;
            case "UiHiddenVersionExport": Config.Preference.Hide.InstanceExport = (bool)value; break;
            case "UiHiddenVersionSave": Config.Preference.Hide.InstanceSave = (bool)value; break;
            case "UiHiddenVersionScreenshot": Config.Preference.Hide.InstanceScreenshot = (bool)value; break;
            case "UiHiddenVersionMod": Config.Preference.Hide.InstanceMod = (bool)value; break;
            case "UiHiddenVersionResourcePack": Config.Preference.Hide.InstanceResourcePack = (bool)value; break;
            case "UiHiddenVersionShader": Config.Preference.Hide.InstanceShader = (bool)value; break;
            case "UiHiddenVersionSchematic": Config.Preference.Hide.InstanceSchematic = (bool)value; break;
            case "UiHiddenVersionServer": Config.Preference.Hide.InstanceServer = (bool)value; break;
            case "UiHiddenFunctionSelect": Config.Preference.Hide.FunctionSelect = (bool)value; break;
            case "UiHiddenFunctionModUpdate": Config.Preference.Hide.FunctionModUpdate = (bool)value; break;
            case "UiHiddenFunctionHidden": Config.Preference.Hide.FunctionHidden = (bool)value; break;
        }
    }

    private void ComboFontChange(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled == 0) Config.Preference.Font = ComboUiFont.SelectedFontTag;
    }

    private void ComboMotdFontChange(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled == 0) Config.Preference.MotdFont = ComboUiMotdFont.SelectedFontTag;
    }

    // 背景图片
    private void BtnUIBgOpen_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenExplorer(ModBase.exePath + @"PCL\Pictures\");
    }

    private void BtnBackgroundRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        BackgroundRefresh(true, true);
    }

    public void BackgroundRefreshUI(bool Show, int Count)
    {
        if (PanBackgroundOpacity is null)
            return;
        if (Show)
        {
            PanBackgroundOpacity.Visibility = Visibility.Visible;
            PanBackgroundBlur.Visibility = Visibility.Visible;
            PanBackgroundSuit.Visibility = Visibility.Visible;
            BtnBackgroundClear.Visibility = Visibility.Visible;
            CheckAutoPauseVideo.Visibility = Visibility.Visible;
            CardBackground.Title = Lang.Text("Setup.Ui.Background.TitleWithCount", Count);
        }
        else
        {
            PanBackgroundOpacity.Visibility = Visibility.Collapsed;
            PanBackgroundBlur.Visibility = Visibility.Collapsed;
            PanBackgroundSuit.Visibility = Visibility.Collapsed;
            BtnBackgroundClear.Visibility = Visibility.Collapsed;
            CheckAutoPauseVideo.Visibility = Visibility.Collapsed;
            CardBackground.Title = Lang.Text("Setup.Ui.Background.TitleDefault");
        }

        CardBackground.TriggerForceResize();
    }

    private void BtnBackgroundClear_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModMain.MyMsgBox(Lang.Text("Setup.Ui.Background.Clear.Confirm.Message"),
                Lang.Text("Common.Dialog.Warning"), Button2: Lang.Text("Common.Action.Cancel"),
                IsWarn: true) == 1)
        {
            ModBase.DeleteDirectory(ModBase.exePath + @"PCL\Pictures");
            BackgroundRefresh(false, true);
            ModMain.Hint(Lang.Text("Setup.Ui.Background.Clear.Success"), ModMain.HintType.Finish);
        }
    }

    /// <summary>
    ///     刷新背景图片及设置页 UI。
    /// </summary>
    /// <param name="IsHint">是否显示刷新提示。</param>
    /// <param name="Refresh">是否刷新图片显示。</param>
    public static void BackgroundRefresh(bool IsHint, bool Refresh)
    {
        try
        {
            // 获取可用的图片文件
            Directory.CreateDirectory(ModBase.exePath + @"PCL\Pictures\");
            var pic = ModBase.EnumerateFiles(ModBase.exePath + @"PCL\Pictures\").Where(file =>
                    !(file.Extension.Equals(".ini", StringComparison.OrdinalIgnoreCase) ||
                      file.Extension.Equals(".db", StringComparison.OrdinalIgnoreCase))).Select(file => file.FullName)
                .ToList();

            // 视频加载异常处理

            EventHandler<ExceptionRoutedEventArgs> videoHandler = (sender, e) =>
            {
                var videoEx = e.ErrorException;
                var videoAddress = ModMain.frmMain.VideoBack.Source.ToString();
                if (ModMain.frmMain.VideoBack.Source is not null)
                {
                    ModVideoBack.VideoStop();

                    if (videoEx.Message.Contains("0xC00D109B"))
                        ModBase.Log(
                            $"""
                             刷新背景内容失败，该视频文件可能并非 H.264（AVC） 格式。
                             你可以尝试使用视频转码工具打开视频文件并设定目标格式为 H.264（AVC） ，然后转码该视频。
                             文件：{videoAddress}
                             """, ModBase.LogLevel.Msgbox);
                    else
                        ModBase.Log(videoEx, $"刷新背景内容失败（{videoAddress}）", ModBase.LogLevel.Msgbox);
                }
            };
            ModMain.frmMain.VideoBack.MediaFailed -= videoHandler;
            ModVideoBack.gamingStateChanged -= ModVideoBack.OnGamingStateChanged;
            ModVideoBack.forcePlayChanged -= ModVideoBack.OnForcePlayChanged;
            ModVideoBack.gamingStateChanged += ModVideoBack.OnGamingStateChanged;
            ModVideoBack.forcePlayChanged += ModVideoBack.OnForcePlayChanged;
            if (!Config.Preference.Background.AutoPauseVideo)
                ModVideoBack.ForcePlay = true;
            // 加载
            if (pic.Count == 0)
            {
                if (Refresh)
                {
                    if (ModMain.frmMain.ImgBack.Visibility == Visibility.Collapsed)
                    {
                        if (IsHint)
                            ModMain.Hint(Lang.Text("Setup.Ui.Background.NoAvailableContent"), ModMain.HintType.Critical);
                    }
                    else
                    {
                        ModMain.frmMain.ImgBack.Visibility = Visibility.Collapsed;
                        if (IsHint)
                            ModMain.Hint(Lang.Text("Setup.Ui.Background.Cleared"), ModMain.HintType.Finish);
                    }
                }

                if (ModMain.frmSetupUI is not null)
                    ModMain.frmSetupUI.BackgroundRefreshUI(false, 0);
            }
            else
            {
                if (Refresh)
                {
                    var address = RandomUtils.PickRandom(pic);
                    try
                    {
                        ModMain.frmMain.ImgBack.Background = null;
                        ModVideoBack.VideoStop();
                        ModBase.Log("[UI] 加载背景内容：" + address);
                        ModMain.frmMain.ImgBack.Background = new MyBitmap(address);
                        _ = Config.Preference.Background.WallpaperSuitMode;
                        ModMain.frmMain.ImgBack.Visibility = Visibility.Visible;
                        if (IsHint)
                                ModMain.Hint(Lang.Text("Setup.Ui.Background.Refresh.Success", ModBase.GetFileNameFromPath(address)), ModMain.HintType.Finish,
                                false);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            ModMain.frmMain.VideoBack.MediaFailed += videoHandler;
                            ModBase.Log(ex, "[UI] 加载背景图片失败" + address);
                            if (ModBase.modeDebug)
                                ModMain.Hint(Lang.Text("Setup.Ui.Background.ImageLoadFailed", address));
                            ModMain.frmMain.ImgBack.Visibility = Visibility.Visible;
                            ModMain.frmMain.VideoBack.Source = new Uri(address, UriKind.Absolute);
                            ModVideoBack.VideoPlay();
                            if (IsHint)
                            ModMain.Hint(Lang.Text("Setup.Ui.Background.Refresh.Success", ModBase.GetFileNameFromPath(address)), ModMain.HintType.Finish,
                                    false);
                        }
                        catch (Exception playEx)
                        {
                            ModBase.Log(playEx, "播放背景内容时出现未知错误：");
                        }
                    }
                }

                if (ModMain.frmSetupUI is not null)
                    ModMain.frmSetupUI.BackgroundRefreshUI(true, pic.Count);
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新背景内容时出现未知错误", ModBase.LogLevel.Feedback);
        }
    }

    // 顶部栏
    private void BtnLogoChange_Click(object sender, MouseButtonEventArgs e)
    {
        var fileName = SystemDialogs.SelectFile("常用图片文件(*.png;*.jpg;*.gif;*.webp)|*.png;*.jpg;*.gif;*.webp", "选择图片");
        if (string.IsNullOrEmpty(fileName))
            return;
        try
        {
            // 拷贝文件
            File.Delete(ModBase.exePath + @"PCL\Logo.png");
            ModBase.CopyFile(fileName, ModBase.exePath + @"PCL\Logo.png");
            // 设置当前显示
            ModMain.frmMain.ImageTitleLogo.Source = null; // 防止因为 Source 属性前后的值相同而不更新 (#5628)
            ModMain.frmMain.ImageTitleLogo.Source = ModBase.exePath + @"PCL\Logo.png";
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("参数无效"))
                ModBase.Log("""
                            改变标题栏图片失败，该图片文件可能并非标准格式。
                            你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。
                            """,
                    ModBase.LogLevel.Msgbox);
            else
                ModBase.Log(ex, "设置标题栏图片失败", ModBase.LogLevel.Msgbox);
            ModMain.frmMain.ImageTitleLogo.Source = null;
        }
    }

    private void RadioLogoType3_Check(object sender, ModBase.RouteEventArgs e)
    {
        if (!(ModAnimation.AniControlEnabled == 0 && e.raiseByMouse))
            return;
        Refresh: ;

        // 已有图片则不再选择
        if (File.Exists(ModBase.exePath + @"PCL\Logo.png"))
        {
            try
            {
                ModMain.frmMain.ImageTitleLogo.Source = null; // 防止因为 Source 属性前后的值相同而不更新 (#5628)
                ModMain.frmMain.ImageTitleLogo.Source = ModBase.exePath + @"PCL\Logo.png";
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("参数无效"))
                    ModBase.Log("""
                                调整标题栏图片失败，该图片文件可能并非标准格式。
                                你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。
                                """,
                        ModBase.LogLevel.Msgbox);
                else
                    ModBase.Log(ex, "调整标题栏图片失败", ModBase.LogLevel.Msgbox);
                ModMain.frmMain.ImageTitleLogo.Source = null;
                e.handled = true;
                try
                {
                    File.Delete(ModBase.exePath + @"PCL\Logo.png");
                }
                catch (Exception exx)
                {
                    ModBase.Log(exx, "清理错误的标题栏图片失败", ModBase.LogLevel.Msgbox);
                }
            }

            return;
        }

        // 没有图片则要求选择
        var fileName = SystemDialogs.SelectFile(Lang.Text("Setup.Ui.ImageFile.Filter"), Lang.Text("Setup.Ui.ImageFile.SelectTitle"));
        if (string.IsNullOrEmpty(fileName))
        {
            ModMain.frmMain.ImageTitleLogo.Source = null;
            e.handled = true;
        }
        else
        {
            try
            {
                // 拷贝文件
                File.Delete(ModBase.exePath + @"PCL\Logo.png");
                ModBase.CopyFile(fileName, ModBase.exePath + @"PCL\Logo.png");
                goto Refresh;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "复制标题栏图片失败", ModBase.LogLevel.Msgbox);
            }
        }
    }

    private void BtnLogoDelete_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            File.Delete(ModBase.exePath + @"PCL\Logo.png");
            RadioLogoType1.SetChecked(true, true);
            ModMain.Hint(Lang.Text("Setup.Ui.Logo.Clear.Success"), ModMain.HintType.Finish);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "清空标题栏图片失败", ModBase.LogLevel.Msgbox);
        }
    }

    // 背景音乐
    private void BtnMusicOpen_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenExplorer(ModBase.exePath + @"PCL\Musics\");
    }

    private void BtnMusicRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        ModMusic.MusicRefreshPlay(true);
    }

    public void MusicRefreshUI()
    {
        if (PanBackgroundOpacity is null)
            return;
        if (ModMusic.musicAllList.Any())
        {
            PanMusicVolume.Visibility = Visibility.Visible;
            PanMusicDetail.Visibility = Visibility.Visible;
            BtnMusicClear.Visibility = Visibility.Visible;
            CardMusic.Title = Lang.Text("Setup.Ui.Music.TitleWithCount", ModBase.EnumerateFiles(ModBase.exePath + @"PCL\Musics\").Count());
        }
        else
        {
            PanMusicVolume.Visibility = Visibility.Collapsed;
            PanMusicDetail.Visibility = Visibility.Collapsed;
            BtnMusicClear.Visibility = Visibility.Collapsed;
            CardMusic.Title = Lang.Text("Setup.Ui.Music.Title");
        }

        CardMusic.TriggerForceResize();
    }

    private void BtnMusicClear_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModMain.MyMsgBox(Lang.Text("Setup.Ui.Music.Clear.Confirm.Message"),
                Lang.Text("Common.Dialog.Warning"), Button2: Lang.Text("Common.Action.Cancel"),
                IsWarn: true) == 1)
            ModBase.RunInThread(() =>
            {
                ModMain.Hint(Lang.Text("Setup.Ui.Music.Deleting"));
                // 停止播放音乐
                ModMusic.musicNAudio = null;
                ModMusic.musicWaitingList = new List<string>();
                ModMusic.musicAllList = new List<string>();
                Thread.Sleep(200);
                // 删除文件
                try
                {
                    ModBase.DeleteDirectory(ModBase.exePath + @"PCL\Musics");
                    // DisableSMTCSupport()
                    ModMain.Hint(Lang.Text("Setup.Ui.Music.Delete.Success"), ModMain.HintType.Finish);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "删除背景音乐失败", ModBase.LogLevel.Msgbox);
                }

                try
                {
                    Directory.CreateDirectory(ModBase.exePath + @"PCL\Musics");
                    ModBase.RunInUi(() => ModMusic.MusicRefreshPlay(false));
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "重建背景音乐文件夹失败", ModBase.LogLevel.Msgbox);
                }
            });
    }

    private void CheckMusicStart_Change(object sender, bool user)
    {
        CheckBoxChange(sender, user);
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (CheckMusicStart.Checked == true)
            CheckMusicStop.Checked = false;
    }

    private void CheckMusicStop_Change()
    {
        if (ModAnimation.AniControlEnabled != 0)
            return;
        if (CheckMusicStop.Checked == true)
            CheckMusicStart.Checked = false;
    }

    // 主页
    private void BtnCustomFile_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (File.Exists(ModBase.exePath + @"PCL\Custom.xaml"))
                if (ModMain.MyMsgBox(Lang.Text("Setup.Ui.Homepage.Docs.OverrideConfirm.Message"), Lang.Text("Setup.Ui.Homepage.Docs.OverrideConfirm.Title"), Lang.Text("Setup.Ui.Homepage.Docs.OverrideConfirm.Continue"), Lang.Text("Common.Action.Cancel"), IsWarn: true) == 2)
                    return;
            ModBase.WriteFile(ModBase.exePath + @"PCL\Custom.xaml", ModBase.GetResourceStream("Resources/Custom.xml"));
            ModMain.Hint(Lang.Text("Setup.Ui.Homepage.Docs.Generated"), ModMain.HintType.Finish);
            ModBase.OpenExplorer(ModBase.exePath + @"PCL\Custom.xaml");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "生成教学文件失败", ModBase.LogLevel.Feedback);
        }
    }

    private void BtnCustomRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.frmLaunchRight.ForceRefresh();
        ModMain.Hint(Lang.Text("Setup.Ui.Homepage.Refresh.Success"), ModMain.HintType.Finish);
    }

    private void BtnCustomTutorial_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.MyMsgBox(
            Lang.Text("Setup.Ui.Homepage.Tutorial.Message"),
            Lang.Text("Setup.Ui.Homepage.Tutorial.Title"));
    }

    // 主题
    private void ThemeColor_Change(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        SetByTag(sender.Tag?.ToString(), sender.SelectedIndex);
        ThemeManager.ThemeRefresh();
    }

    // 赞助
    private void BtnLauncherDonate_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenWebsite("https://afdian.com/a/LTCat");
    }

    // 滑动条
    private void SliderLoad()
    {
        SliderMusicVolume.getHintText = new Func<object, object>(v =>
            Lang.Number(Math.Ceiling(Convert.ToDouble(v) * 0.1d) / 100d, "P0"));
        SliderLauncherOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number(Math.Round(40 + Convert.ToDouble(v) * 0.1d) / 100d, "P0"));
        SliderBackgroundOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number(Math.Round(Convert.ToDouble(v) * 0.1d) / 100d, "P0"));
        SliderBackgroundBlur.getHintText = new Func<object, object>(v => Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(v), "N0")));
        SliderBlurValue.getHintText = new Func<object, object>(v => Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(v), "N0")));
        SliderBlurSamplingRate.getHintText = new Func<object, object>(v => Lang.Number(Convert.ToDouble(v) / 100d, "P0"));
    }

    private void BtnHomepageMarket_Click(object sender, ModBase.RouteEventArgs e)
    {
        ModMain.frmMain.PageChange(new FormMain.PageStackData { page = FormMain.PageType.HomePageMarket });
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

    private static bool _HiddenForceShow;

    /// <summary>
    ///     是否强制显示被禁用的功能。
    /// </summary>
    public static bool HiddenForceShow
    {
        get => _HiddenForceShow;
        set
        {
            _HiddenForceShow = value;
            HiddenRefresh();
        }
    }

    /// <summary>
    ///     更新功能隐藏带来的显示变化。
    /// </summary>
    public static void HiddenRefresh()
    {
        if (ModMain.frmMain.PanTitleSelect is null || !ModMain.frmMain.PanTitleSelect.IsLoaded)
            return;
        try
        {
            // 获取配置组引用以缩短代码
            var conf = Config.Preference.Hide;

            // 顶部栏：下载、设置、工具
            var isAllTitleHidden = !HiddenForceShow && conf.PageDownload && conf.PageSetup && conf.PageTools;

            if (isAllTitleHidden)
            {
                ModMain.frmMain.PanTitleSelect.Visibility = Visibility.Collapsed;
            }
            else
            {
                ModMain.frmMain.PanTitleSelect.Visibility = Visibility.Visible;
                ModMain.frmMain.BtnTitleSelect1.Visibility = !HiddenForceShow && conf.PageDownload
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmMain.BtnTitleSelect2.Visibility =
                    !HiddenForceShow && conf.PageSetup ? Visibility.Collapsed : Visibility.Visible;
                ModMain.frmMain.BtnTitleSelect3.Visibility =
                    !HiddenForceShow && conf.PageTools ? Visibility.Collapsed : Visibility.Visible;
            }

            // 功能隐藏设置卡片
            if (ModMain.frmSetupUI is not null)
            {
                ModMain.frmSetupUI.CardSwitch.Visibility = !HiddenForceShow && conf.FunctionHidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmSetupUI.CardSwitch.Title = HiddenForceShow ? Lang.Text("Setup.Ui.FeatureHide.TitleTemporarilyDisabled") : Lang.Text("Setup.Ui.FeatureHide.Title");
            }

            // 设置子页面 (FrmSetupLeft)
            if (ModMain.frmSetupLeft is not null)
            {
                ModMain.frmSetupLeft.ItemLaunch.Visibility =
                    !HiddenForceShow && conf.SetupLaunch ? Visibility.Collapsed : Visibility.Visible;
                ModMain.frmSetupLeft.ItemUI.Visibility =
                    !HiddenForceShow && conf.SetupUi ? Visibility.Collapsed : Visibility.Visible;
                ModMain.frmSetupLeft.ItemLauncherLanguage.Visibility = !HiddenForceShow && conf.SetupLauncherLanguage
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmSetupLeft.ItemGameManage.Visibility = !HiddenForceShow && conf.SetupGameManage
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmSetupLeft.ItemLauncherMisc.Visibility = !HiddenForceShow && conf.SetupLauncherMisc
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmSetupLeft.ItemJava.Visibility =
                    !HiddenForceShow && conf.SetupJava ? Visibility.Collapsed : Visibility.Visible;
                ModMain.frmSetupLeft.ItemUpdate.Visibility =
                    !HiddenForceShow && conf.SetupUpdate ? Visibility.Collapsed : Visibility.Visible;
                ModMain.frmSetupLeft.ItemGameLink.Visibility = !HiddenForceShow && conf.SetupGameLink
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmSetupLeft.ItemAbout.Visibility =
                    !HiddenForceShow && conf.SetupAbout ? Visibility.Collapsed : Visibility.Visible;
                ModMain.frmSetupLeft.ItemFeedback.Visibility = !HiddenForceShow && conf.SetupFeedback
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmSetupLeft.ItemLog.Visibility =
                    !HiddenForceShow && conf.SetupLog ? Visibility.Collapsed : Visibility.Visible;

                var categories = new[]
                {
                    (ModMain.frmSetupLeft.TextGameCategory,
                        !(conf.SetupLaunch && conf.SetupJava && conf.SetupGameManage)),
                    (ModMain.frmSetupLeft.TextToolsCategory, !conf.SetupGameLink),
                    (ModMain.frmSetupLeft.TextLauncherCategory, !(conf.SetupUi && conf.SetupLauncherLanguage && conf.SetupLauncherMisc)),
                    (ModMain.frmSetupLeft.TextAboutCategory,
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
                ModMain.frmSetupLeft.PanItem.Visibility =
                    setupCount < 2 && !HiddenForceShow ? Visibility.Collapsed : Visibility.Visible;
            }

            // 工具子页面 (FrmToolsLeft)
            if (ModMain.frmToolsLeft is not null)
            {
                ModMain.frmToolsLeft.ItemGameLink.Visibility = !HiddenForceShow && conf.ToolsGameLink
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmToolsLeft.ItemLauncherHelp.Visibility =
                    !HiddenForceShow && conf.ToolsHelp ? Visibility.Collapsed : Visibility.Visible;
                ModMain.frmToolsLeft.ItemTest.Visibility =
                    !HiddenForceShow && conf.ToolsTest ? Visibility.Collapsed : Visibility.Visible;
                
                // 处理分类标题
                var isGameLinkVisible = (!HiddenForceShow && !conf.ToolsGameLink) || HiddenForceShow;
                ModMain.frmToolsLeft.TextGameLinkCategory.Visibility = isGameLinkVisible ? Visibility.Visible : Visibility.Collapsed;
                if (isGameLinkVisible) ModMain.frmToolsLeft.TextGameLinkCategory.Opacity = 0.6;

                var isToolsVisible = (!HiddenForceShow && (!conf.ToolsHelp || !conf.ToolsTest)) || HiddenForceShow;
                ModMain.frmToolsLeft.TextToolsCategory.Visibility = isToolsVisible ? Visibility.Visible : Visibility.Collapsed;
                if (isToolsVisible) ModMain.frmToolsLeft.TextToolsCategory.Opacity = 0.6;
                
                // 统计工具页可用项数量
                var toolsCount = 0;
                if (!conf.ToolsGameLink)
                    toolsCount += 1;
                if (!conf.ToolsHelp)
                    toolsCount += 1;
                if (!conf.ToolsTest)
                    toolsCount += 1;
                ModMain.frmToolsLeft.PanItem.Visibility =
                    toolsCount < 2 && !HiddenForceShow ? Visibility.Collapsed : Visibility.Visible;
            }

            // 其他入口刷新
            if (ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSelect)
                ModMain.frmSelectRight.BtnEmptyDownload_Loaded();
            if (ModMain.frmMain.pageCurrent == FormMain.PageType.Launch)
                ModMain.frmLaunchLeft.RefreshButtonsUI();
            if (ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSetup &&
                ModMain.frmInstanceModDisabled is not null)
                ModMain.frmInstanceModDisabled.BtnDownload_Loaded();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新功能隐藏项目失败", ModBase.LogLevel.Feedback);
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
        CheckHiddenToolsHelp.Checked = isChecked;
        CheckHiddenToolsTest.Checked = isChecked;
    }

    private void HiddenToolsSub(object sender, bool user)
    {
        if (!user)
            return;
        var conf = Config.Preference.Hide;
        var allChecked = conf.ToolsGameLink && conf.ToolsHelp && conf.ToolsTest;
        CheckHiddenPageTools.Checked = allChecked;
    }

    // 警告提示
    private void HiddenHint(object sender, bool user)
    {
        if (ModAnimation.AniControlEnabled == 0 && sender is MyCheckBox checkBox && checkBox.Checked == true)
            ModMain.Hint(Lang.Text("Setup.Ui.FeatureHide.TemporaryHint"));
    }

    #endregion
}
