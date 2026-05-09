using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.Utils;

namespace PCL;

public partial class PageSetupUI
{
    public string[] ThemeColors => Basics.IsAprilFool 
        ? ["天空蓝", "龙猫蓝", "死机蓝", "HMCL"]
        : ["天空蓝", "龙猫蓝", "死机蓝"];
    
    public new bool IsLoaded;

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
        if (IsLoaded)
            return;
        IsLoaded = true;

        SliderLoad();

        PanLauncherHide.Visibility = Visibility.Visible;
    }

    public void Reload()
    {
        try
        {
            // 启动器
            SliderLauncherOpacity.Value = Conversions.ToInteger(Config.Preference.Theme.WindowOpacity);
            CheckLauncherLogo.Checked = (bool?)Config.Preference.ShowStartupLogo;
            ComboDarkMode.SelectedIndex = Conversions.ToInteger(Config.Preference.Theme.ColorMode);
            ComboDarkColor.SelectedIndex = Conversions.ToInteger(Config.Preference.Theme.DarkColor);
            ComboLightColor.SelectedIndex = Conversions.ToInteger(Config.Preference.Theme.LightColor);
            CheckShowLaunchingHint.Checked = (bool?)Config.Preference.ShowLaunchingHint;

            // 字体设置
            ComboUiFont.SelectedFontTag = Conversions.ToString(Config.Preference.Font);
            ComboUiMotdFont.SelectedFontTag = Conversions.ToString(Config.Preference.MotdFont);

            CheckBlur.Checked = (bool?)Config.Preference.Blur.IsEnabled;
            SliderBlurValue.Value = Conversions.ToInteger(Config.Preference.Blur.Radius);
            SliderBlurSamplingRate.Value = Conversions.ToInteger(Config.Preference.Blur.SamplingRate);
            ComboBlurType.SelectedIndex = Conversions.ToInteger(Config.Preference.Blur.KernelType);
            PanBlurValue.Visibility = CheckBlur.Checked == true ? Visibility.Visible : Visibility.Collapsed;
            CheckLockWindowSize.Checked = (bool?)Config.Preference.LockWindowSize;

            // 背景图片
            SliderBackgroundOpacity.Value = Conversions.ToInteger(Config.Preference.Background.WallpaperOpacity);
            SliderBackgroundBlur.Value = Conversions.ToInteger(Config.Preference.Background.WallpaperBlurRadius);
            ComboBackgroundSuit.SelectedIndex = Conversions.ToInteger(Config.Preference.Background.WallpaperSuitMode);
            CheckBackgroundColorful.Checked = (bool?)Config.Preference.Background.BackgroundColorful;
            var autoPauseVideo = Config.Preference.Background.AutoPauseVideo;
            CheckAutoPauseVideo.Checked = (bool?)autoPauseVideo;
            if (ModVideoBack.IsGaming)
                if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(autoPauseVideo, true, false)))
                    BtnBackgroundRefresh.IsEnabled = false;

            BackgroundRefresh(false, false);

            // 标题栏
            ((MyRadioBox)FindName(
                    Conversions.ToString(Operators.ConcatenateObject("RadioLogoType",
                        Config.Preference.WindowTitleType))))
                .Checked = true;
            CheckLogoLeft.Visibility = RadioLogoType0.Checked ? Visibility.Visible : Visibility.Collapsed;
            PanLogoText.Visibility = RadioLogoType2.Checked ? Visibility.Visible : Visibility.Collapsed;
            PanLogoChange.Visibility = RadioLogoType3.Checked ? Visibility.Visible : Visibility.Collapsed;
            TextLogoText.Text = Conversions.ToString(Config.Preference.WindowTitleCustomText);
            CheckLogoLeft.Checked = (bool?)Config.Preference.TopBarLeftAlign;

            // 背景音乐
            CheckMusicRandom.Checked = (bool?)Config.Preference.Music.ShufflePlayback;
            CheckMusicAuto.Checked = (bool?)Config.Preference.Music.StartOnStartup;
            CheckMusicStop.Checked = (bool?)Config.Preference.Music.StopInGame;
            CheckMusicStart.Checked = (bool?)Config.Preference.Music.StartInGame;
            CheckMusicSMTC.Checked = (bool?)Config.Preference.Music.EnableSMTC;
            SliderMusicVolume.Value = Conversions.ToInteger(Config.Preference.Music.Volume);
            MusicRefreshUI();

            // 主页
            try
            {
                ComboCustomPreset.SelectedIndex = Conversions.ToInteger(Config.Preference.Homepage.SelectedPreset);
            }
            catch
            {
                ModBase.Setup.Reset("UiCustomPreset");
            }

            ((MyRadioBox)FindName(Conversions.ToString(Operators.ConcatenateObject("RadioCustomType",
                ModBase.Setup.Load("UiCustomType", true))))).Checked = true;
            TextCustomNet.Text = Conversions.ToString(Config.Preference.Homepage.CustomUrl);

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
            ModBase.Log(ex, "个性化设置项存在异常，已被自动重置", ModBase.LogLevel.Msgbox);
            Reset();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "重载个性化设置时出错", ModBase.LogLevel.Feedback);
        }
    }

    // 初始化
    public void Reset()
    {
        try
        {
            Config.Preference.Reset();
            ModBase.Log("[Setup] 已初始化个性化设置！");
            ModMain.Hint("已初始化个性化设置", ModMain.HintType.Finish, false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化个性化设置失败", ModBase.LogLevel.Msgbox);
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void SliderChange(object senderRaw, bool user)
    {
        var sender = (MySlider)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag?.ToString(), sender.Value);
    }

    private void ComboChange(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag?.ToString(), sender.SelectedIndex);
    }

    private void CheckBoxChange(object senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw;
        // 仅在动画未运行或初始化完成时保存设置，防止初始化时的触发导致重复写入
        if (ModAnimation.AniControlEnabled == 0) ModBase.Setup.Set(sender.Tag?.ToString(), sender.Checked);
    }

    private void TextBoxChange(object senderRaw, RoutedEventArgs e)
    {
        var sender = (MyTextBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            ModBase.Setup.Set(sender.Tag?.ToString(), sender.Text);
    }

    private void RadioBoxChange(object senderRaw, ModBase.RouteEventArgs e)
    {
        var sender = (MyRadioBox)senderRaw;
        var gotCfg = sender.Tag?.ToString()?.Split("/") ?? Array.Empty<string>();
        if (ModAnimation.AniControlEnabled == 0 && gotCfg.Length >= 2)
            ModBase.Setup.Set(gotCfg[0], int.Parse(gotCfg[1]));
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
        ModBase.OpenExplorer(ModBase.ExePath + @"PCL\Pictures\");
    }

    private void BtnBackgroundRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        BackgroundRefresh(true, true);
    }

    public void BackgroundRefreshUI(bool Show, int Count)
    {
        if (PanBackgroundOpacity == null)
            return;
        if (Show)
        {
            PanBackgroundOpacity.Visibility = Visibility.Visible;
            PanBackgroundBlur.Visibility = Visibility.Visible;
            PanBackgroundSuit.Visibility = Visibility.Visible;
            BtnBackgroundClear.Visibility = Visibility.Visible;
            CheckAutoPauseVideo.Visibility = Visibility.Visible;
            CardBackground.Title = $"背景图片/视频（{Count} 张）";
        }
        else
        {
            PanBackgroundOpacity.Visibility = Visibility.Collapsed;
            PanBackgroundBlur.Visibility = Visibility.Collapsed;
            PanBackgroundSuit.Visibility = Visibility.Collapsed;
            BtnBackgroundClear.Visibility = Visibility.Collapsed;
            CheckAutoPauseVideo.Visibility = Visibility.Collapsed;
            CardBackground.Title = "背景图片/视频";
        }

        CardBackground.TriggerForceResize();
    }

    private void BtnBackgroundClear_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModMain.MyMsgBox("""
                             即将删除背景内容文件夹中的所有文件。
                             此操作不可撤销，是否确定？
                             """, "警告", Button2: "取消",
                IsWarn: true) == 1)
        {
            ModBase.DeleteDirectory(ModBase.ExePath + @"PCL\Pictures");
            BackgroundRefresh(false, true);
            ModMain.Hint("背景内容已清空！", ModMain.HintType.Finish);
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
            Directory.CreateDirectory(ModBase.ExePath + @"PCL\Pictures\");
            var Pic = ModBase.EnumerateFiles(ModBase.ExePath + @"PCL\Pictures\").Where(file =>
                    !(file.Extension.Equals(".ini", StringComparison.OrdinalIgnoreCase) ||
                      file.Extension.Equals(".db", StringComparison.OrdinalIgnoreCase))).Select(file => file.FullName)
                .ToList();

            // 视频加载异常处理

            EventHandler<ExceptionRoutedEventArgs> videoHandler = (sender, e) =>
            {
                var videoEx = e.ErrorException;
                var videoAddress = ModMain.FrmMain.VideoBack.Source.ToString();
                if (ModMain.FrmMain.VideoBack.Source is not null)
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
            ModMain.FrmMain.VideoBack.MediaFailed -= videoHandler;
            ModVideoBack.GamingStateChanged -= ModVideoBack.OnGamingStateChanged;
            ModVideoBack.ForcePlayChanged -= ModVideoBack.OnForcePlayChanged;
            ModVideoBack.GamingStateChanged += ModVideoBack.OnGamingStateChanged;
            ModVideoBack.ForcePlayChanged += ModVideoBack.OnForcePlayChanged;
            if (Conversions.ToBoolean(
                    Operators.ConditionalCompareObjectEqual(Config.Preference.Background.AutoPauseVideo, false, false)))
                ModVideoBack.ForcePlay = true;
            // 加载
            if (Pic.Count == 0)
            {
                if (Refresh)
                {
                    if (ModMain.FrmMain.ImgBack.Visibility == Visibility.Collapsed)
                    {
                        if (IsHint)
                            ModMain.Hint("未检测到可用背景内容！", ModMain.HintType.Critical);
                    }
                    else
                    {
                        ModMain.FrmMain.ImgBack.Visibility = Visibility.Collapsed;
                        if (IsHint)
                            ModMain.Hint("背景内容已清除！", ModMain.HintType.Finish);
                    }
                }

                if (!(ModMain.FrmSetupUI == null))
                    ModMain.FrmSetupUI.BackgroundRefreshUI(false, 0);
            }
            else
            {
                if (Refresh)
                {
                    var Address = RandomUtils.PickRandom(Pic);
                    try
                    {
                        ModMain.FrmMain.ImgBack.Background = null;
                        ModVideoBack.VideoStop();
                        ModBase.Log("[UI] 加载背景内容：" + Address);
                        ModMain.FrmMain.ImgBack.Background = new MyBitmap(Address);
                        ModBase.Setup.Load("UiBackgroundSuit", true);
                        ModMain.FrmMain.ImgBack.Visibility = Visibility.Visible;
                        if (IsHint)
                            ModMain.Hint("背景内容已刷新：" + ModBase.GetFileNameFromPath(Address), ModMain.HintType.Finish,
                                false);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            ModMain.FrmMain.VideoBack.MediaFailed += videoHandler;
                            ModBase.Log(ex, "[UI] 加载背景图片失败" + Address);
                            if (ModBase.ModeDebug)
                                ModMain.Hint("图片加载失败，尝试将文件作为视频播放：" + Address);
                            ModMain.FrmMain.ImgBack.Visibility = Visibility.Visible;
                            ModMain.FrmMain.VideoBack.Source = new Uri(Address, UriKind.Absolute);
                            ModVideoBack.VideoPlay();
                            if (IsHint)
                                ModMain.Hint("背景内容已刷新：" + ModBase.GetFileNameFromPath(Address), ModMain.HintType.Finish,
                                    false);
                        }
                        catch (Exception playEx)
                        {
                            ModBase.Log(playEx, "播放背景内容时出现未知错误：");
                        }
                    }
                }

                if (!(ModMain.FrmSetupUI == null))
                    ModMain.FrmSetupUI.BackgroundRefreshUI(true, Pic.Count);
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
        var FileName = SystemDialogs.SelectFile("常用图片文件(*.png;*.jpg;*.gif;*.webp)|*.png;*.jpg;*.gif;*.webp", "选择图片");
        if (string.IsNullOrEmpty(FileName))
            return;
        try
        {
            // 拷贝文件
            File.Delete(ModBase.ExePath + @"PCL\Logo.png");
            ModBase.CopyFile(FileName, ModBase.ExePath + @"PCL\Logo.png");
            // 设置当前显示
            ModMain.FrmMain.ImageTitleLogo.Source = null; // 防止因为 Source 属性前后的值相同而不更新 (#5628)
            ModMain.FrmMain.ImageTitleLogo.Source = ModBase.ExePath + @"PCL\Logo.png";
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
            ModMain.FrmMain.ImageTitleLogo.Source = null;
        }
    }

    private void RadioLogoType3_Check(object sender, ModBase.RouteEventArgs e)
    {
        if (!(ModAnimation.AniControlEnabled == 0 && e.RaiseByMouse))
            return;
        Refresh: ;

        // 已有图片则不再选择
        if (File.Exists(ModBase.ExePath + @"PCL\Logo.png"))
        {
            try
            {
                ModMain.FrmMain.ImageTitleLogo.Source = null; // 防止因为 Source 属性前后的值相同而不更新 (#5628)
                ModMain.FrmMain.ImageTitleLogo.Source = ModBase.ExePath + @"PCL\Logo.png";
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
                ModMain.FrmMain.ImageTitleLogo.Source = null;
                e.Handled = true;
                try
                {
                    File.Delete(ModBase.ExePath + @"PCL\Logo.png");
                }
                catch (Exception exx)
                {
                    ModBase.Log(exx, "清理错误的标题栏图片失败", ModBase.LogLevel.Msgbox);
                }
            }

            return;
        }

        // 没有图片则要求选择
        var FileName = SystemDialogs.SelectFile("常用图片文件(*.png;*.jpg;*.gif;*.webp)|*.png;*.jpg;*.gif;*.webp", "选择图片");
        if (string.IsNullOrEmpty(FileName))
        {
            ModMain.FrmMain.ImageTitleLogo.Source = null;
            e.Handled = true;
        }
        else
        {
            try
            {
                // 拷贝文件
                File.Delete(ModBase.ExePath + @"PCL\Logo.png");
                ModBase.CopyFile(FileName, ModBase.ExePath + @"PCL\Logo.png");
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
            File.Delete(ModBase.ExePath + @"PCL\Logo.png");
            RadioLogoType1.SetChecked(true, true);
            ModMain.Hint("标题栏图片已清空！", ModMain.HintType.Finish);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "清空标题栏图片失败", ModBase.LogLevel.Msgbox);
        }
    }

    // 背景音乐
    private void BtnMusicOpen_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenExplorer(ModBase.ExePath + @"PCL\Musics\");
    }

    private void BtnMusicRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        ModMusic.MusicRefreshPlay(true);
    }

    public void MusicRefreshUI()
    {
        if (PanBackgroundOpacity is null)
            return;
        if (ModMusic.MusicAllList.Any())
        {
            PanMusicVolume.Visibility = Visibility.Visible;
            PanMusicDetail.Visibility = Visibility.Visible;
            BtnMusicClear.Visibility = Visibility.Visible;
            CardMusic.Title = $"背景音乐（{ModBase.EnumerateFiles(ModBase.ExePath + @"PCL\Musics\").Count()} 首）";
        }
        else
        {
            PanMusicVolume.Visibility = Visibility.Collapsed;
            PanMusicDetail.Visibility = Visibility.Collapsed;
            BtnMusicClear.Visibility = Visibility.Collapsed;
            CardMusic.Title = "背景音乐";
        }

        CardMusic.TriggerForceResize();
    }

    private void BtnMusicClear_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModMain.MyMsgBox("""
                             即将删除背景音乐文件夹中的所有文件。
                             此操作不可撤销，是否确定？
                             """, "警告", Button2: "取消",
                IsWarn: true) == 1)
            ModBase.RunInThread(() =>
            {
                ModMain.Hint("正在删除背景音乐……");
                // 停止播放音乐
                ModMusic.MusicNAudio = null;
                ModMusic.MusicWaitingList = new List<string>();
                ModMusic.MusicAllList = new List<string>();
                Thread.Sleep(200);
                // 删除文件
                try
                {
                    ModBase.DeleteDirectory(ModBase.ExePath + @"PCL\Musics");
                    // DisableSMTCSupport()
                    ModMain.Hint("背景音乐已删除！", ModMain.HintType.Finish);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "删除背景音乐失败", ModBase.LogLevel.Msgbox);
                }

                try
                {
                    Directory.CreateDirectory(ModBase.ExePath + @"PCL\Musics");
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
            if (File.Exists(ModBase.ExePath + @"PCL\Custom.xaml"))
                if (ModMain.MyMsgBox("当前已存在布局文件，继续生成教学文件将会覆盖现有布局文件！", "覆盖确认", "继续", "取消", IsWarn: true) == 2)
                    return;
            ModBase.WriteFile(ModBase.ExePath + @"PCL\Custom.xaml", ModBase.GetResourceStream("Resources/Custom.xml"));
            ModMain.Hint("教学文件已生成！", ModMain.HintType.Finish);
            ModBase.OpenExplorer(ModBase.ExePath + @"PCL\Custom.xaml");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "生成教学文件失败", ModBase.LogLevel.Feedback);
        }
    }

    private void BtnCustomRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.FrmLaunchRight.ForceRefresh();
        ModMain.Hint("已刷新主页！", ModMain.HintType.Finish);
    }

    private void BtnCustomTutorial_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.MyMsgBox(
            """
            1. 点击 生成教学文件 按钮，这会在 PCL 文件夹下生成 Custom.xaml 布局文件。
            2. 使用记事本等工具打开这个文件并进行修改，修改完记得保存。
            3. 点击 刷新主页 按钮，查看主页现在长啥样了。

            你可以在生成教学文件后直接刷新主页，对照着进行修改，更有助于理解。
            直接将主页文件拖进 PCL 窗口也可以快捷加载。
            """, "主页自定义教程");
    }

    // 主题
    private void ThemeColor_Change(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        ModBase.Setup.Set(sender.Tag?.ToString(), sender.SelectedIndex);
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
        SliderMusicVolume.GetHintText = new Func<object, object>(v =>
            Operators.ConcatenateObject(Math.Ceiling(Convert.ToDouble(v) * 0.1d), "%"));
        SliderLauncherOpacity.GetHintText = new Func<object, object>(v =>
            Operators.ConcatenateObject(Math.Round(40 + Convert.ToDouble(v) * 0.1d), "%"));
        SliderBackgroundOpacity.GetHintText = new Func<object, object>(v =>
            Operators.ConcatenateObject(Math.Round(Convert.ToDouble(v) * 0.1d), "%"));
        SliderBackgroundBlur.GetHintText = new Func<object, object>(v => Operators.ConcatenateObject(v, " 像素"));
        SliderBlurValue.GetHintText = new Func<object, object>(v => Operators.ConcatenateObject(v, " 像素"));
        SliderBlurSamplingRate.GetHintText = new Func<object, object>(v => Operators.ConcatenateObject(v, "%"));
    }

    private void BtnHomepageMarket_Click(object sender, ModBase.RouteEventArgs e)
    {
        ModMain.FrmMain.PageChange(new FormMain.PageStackData { Page = FormMain.PageType.HomePageMarket });
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
        if (ModMain.FrmMain.PanTitleSelect is null || !ModMain.FrmMain.PanTitleSelect.IsLoaded)
            return;
        try
        {
            // 获取配置组引用以缩短代码
            var conf = Config.Preference.Hide;

            // 顶部栏：下载、设置、工具
            var IsAllTitleHidden = !HiddenForceShow && conf.PageDownload && conf.PageSetup && conf.PageTools;

            if (IsAllTitleHidden)
            {
                ModMain.FrmMain.PanTitleSelect.Visibility = Visibility.Collapsed;
            }
            else
            {
                ModMain.FrmMain.PanTitleSelect.Visibility = Visibility.Visible;
                ModMain.FrmMain.BtnTitleSelect1.Visibility = !HiddenForceShow && conf.PageDownload
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.FrmMain.BtnTitleSelect2.Visibility =
                    !HiddenForceShow && conf.PageSetup ? Visibility.Collapsed : Visibility.Visible;
                ModMain.FrmMain.BtnTitleSelect3.Visibility =
                    !HiddenForceShow && conf.PageTools ? Visibility.Collapsed : Visibility.Visible;
            }

            // 功能隐藏设置卡片
            if (ModMain.FrmSetupUI is not null)
            {
                ModMain.FrmSetupUI.CardSwitch.Visibility = !HiddenForceShow && conf.FunctionHidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.FrmSetupUI.CardSwitch.Title = HiddenForceShow ? "功能隐藏（已暂时关闭，按 F12 以重新启用）" : "功能隐藏";
            }

            // 设置子页面 (FrmSetupLeft)
            if (ModMain.FrmSetupLeft is not null)
            {
                ModMain.FrmSetupLeft.ItemLaunch.Visibility =
                    !HiddenForceShow && conf.SetupLaunch ? Visibility.Collapsed : Visibility.Visible;
                ModMain.FrmSetupLeft.ItemUI.Visibility =
                    !HiddenForceShow && conf.SetupUi ? Visibility.Collapsed : Visibility.Visible;
                ModMain.FrmSetupLeft.ItemGameManage.Visibility = !HiddenForceShow && conf.SetupGameManage
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.FrmSetupLeft.ItemLauncherMisc.Visibility = !HiddenForceShow && conf.SetupLauncherMisc
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.FrmSetupLeft.ItemJava.Visibility =
                    !HiddenForceShow && conf.SetupJava ? Visibility.Collapsed : Visibility.Visible;
                ModMain.FrmSetupLeft.ItemUpdate.Visibility =
                    !HiddenForceShow && conf.SetupUpdate ? Visibility.Collapsed : Visibility.Visible;
                ModMain.FrmSetupLeft.ItemGameLink.Visibility = !HiddenForceShow && conf.SetupGameLink
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.FrmSetupLeft.ItemAbout.Visibility =
                    !HiddenForceShow && conf.SetupAbout ? Visibility.Collapsed : Visibility.Visible;
                ModMain.FrmSetupLeft.ItemFeedback.Visibility = !HiddenForceShow && conf.SetupFeedback
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.FrmSetupLeft.ItemLog.Visibility =
                    !HiddenForceShow && conf.SetupLog ? Visibility.Collapsed : Visibility.Visible;

                var categories = new[]
                {
                    (ModMain.FrmSetupLeft.TextGameCategory,
                        !(conf.SetupLaunch && conf.SetupJava && conf.SetupGameManage)),
                    (ModMain.FrmSetupLeft.TextToolsCategory, !conf.SetupGameLink),
                    (ModMain.FrmSetupLeft.TextLauncherCategory, !(conf.SetupUi && conf.SetupLauncherMisc)),
                    (ModMain.FrmSetupLeft.TextAboutCategory,
                        !(conf.SetupAbout && conf.SetupUpdate && conf.SetupFeedback && conf.SetupLog))
                };

                foreach (var category in categories)
                {
                    var isVisible = category.Item2 || HiddenForceShow;
                    category.Item1.Visibility =
                        Conversions.ToBoolean(isVisible) ? Visibility.Visible : Visibility.Collapsed;
                    if (Conversions.ToBoolean(isVisible))
                        category.Item1.Opacity = 0.6d;
                }

                // 统计设置页可用项数量
                var SetupCount = 0;
                if (!conf.SetupLaunch)
                    SetupCount += 1;
                if (!conf.SetupUi)
                    SetupCount += 1;
                if (!conf.SetupGameManage)
                    SetupCount += 1;
                if (!conf.SetupLauncherMisc)
                    SetupCount += 1;
                if (!conf.SetupJava)
                    SetupCount += 1;
                if (!conf.SetupUpdate)
                    SetupCount += 1;
                if (!conf.SetupGameLink)
                    SetupCount += 1;
                if (!conf.SetupAbout)
                    SetupCount += 1;
                if (!conf.SetupFeedback)
                    SetupCount += 1;
                if (!conf.SetupLog)
                    SetupCount += 1;
                ModMain.FrmSetupLeft.PanItem.Visibility =
                    SetupCount < 2 && !HiddenForceShow ? Visibility.Collapsed : Visibility.Visible;
            }

            // 工具子页面 (FrmToolsLeft)
            if (ModMain.FrmToolsLeft is not null)
            {
                ModMain.FrmToolsLeft.ItemGameLink.Visibility = !HiddenForceShow && conf.ToolsGameLink
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.FrmToolsLeft.ItemLauncherHelp.Visibility =
                    !HiddenForceShow && conf.ToolsHelp ? Visibility.Collapsed : Visibility.Visible;
                ModMain.FrmToolsLeft.ItemTest.Visibility =
                    !HiddenForceShow && conf.ToolsTest ? Visibility.Collapsed : Visibility.Visible;
                
                // 处理分类标题
                var isGameLinkVisible = (!HiddenForceShow && !conf.ToolsGameLink) || HiddenForceShow;
                ModMain.FrmToolsLeft.TextGameLinkCategory.Visibility = isGameLinkVisible ? Visibility.Visible : Visibility.Collapsed;
                if (isGameLinkVisible) ModMain.FrmToolsLeft.TextGameLinkCategory.Opacity = 0.6;

                var isToolsVisible = (!HiddenForceShow && (!conf.ToolsHelp || !conf.ToolsTest)) || HiddenForceShow;
                ModMain.FrmToolsLeft.TextToolsCategory.Visibility = isToolsVisible ? Visibility.Visible : Visibility.Collapsed;
                if (isToolsVisible) ModMain.FrmToolsLeft.TextToolsCategory.Opacity = 0.6;
                
                // 统计工具页可用项数量
                var ToolsCount = 0;
                if (!conf.ToolsGameLink)
                    ToolsCount += 1;
                if (!conf.ToolsHelp)
                    ToolsCount += 1;
                if (!conf.ToolsTest)
                    ToolsCount += 1;
                ModMain.FrmToolsLeft.PanItem.Visibility =
                    ToolsCount < 2 && !HiddenForceShow ? Visibility.Collapsed : Visibility.Visible;
            }

            // 其他入口刷新
            if (ModMain.FrmMain.PageCurrent == FormMain.PageType.InstanceSelect)
                ModMain.FrmSelectRight.BtnEmptyDownload_Loaded();
            if (ModMain.FrmMain.PageCurrent == FormMain.PageType.Launch)
                ModMain.FrmLaunchLeft.RefreshButtonsUI();
            if (ModMain.FrmMain.PageCurrent == FormMain.PageType.InstanceSetup &&
                ModMain.FrmInstanceModDisabled is not null)
                ModMain.FrmInstanceModDisabled.BtnDownload_Loaded();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新功能隐藏项目失败", ModBase.LogLevel.Feedback);
        }
    }

    // ================= 设置页面协同 =================
    private void HiddenSetupMain()
    {
        var IsChecked = (bool)CheckHiddenPageSetup.Checked;
        CheckHiddenSetupLaunch.Checked = IsChecked;
        CheckHiddenSetupUI.Checked = IsChecked;
        CheckHiddenSetupGameManage.Checked = IsChecked;
        CheckHiddenLauncherMisc.Checked = IsChecked;
        CheckHiddenSetupJava.Checked = IsChecked;
        CheckHiddenSetupUpdate.Checked = IsChecked;
        CheckHiddenSetupGameLink.Checked = IsChecked;
        CheckHiddenSetupAbout.Checked = IsChecked;
        CheckHiddenSetupFeedback.Checked = IsChecked;
        CheckHiddenSetupLog.Checked = IsChecked;
    }

    // ================= 设置页面协同 =================
    private void HiddenSetupMain(object sender, bool user)
    {
        if (!user)
            return; // 仅处理用户点击，防止死循环
        var IsChecked = (bool)CheckHiddenPageSetup.Checked;
        CheckHiddenSetupLaunch.Checked = IsChecked;
        CheckHiddenSetupUI.Checked = IsChecked;
        CheckHiddenSetupGameManage.Checked = IsChecked;
        CheckHiddenLauncherMisc.Checked = IsChecked;
        CheckHiddenSetupJava.Checked = IsChecked;
        CheckHiddenSetupUpdate.Checked = IsChecked;
        CheckHiddenSetupGameLink.Checked = IsChecked;
        CheckHiddenSetupAbout.Checked = IsChecked;
        CheckHiddenSetupFeedback.Checked = IsChecked;
        CheckHiddenSetupLog.Checked = IsChecked;
    }

    private void HiddenSetupSub(object sender, bool user)
    {
        if (!user)
            return;
        var conf = Config.Preference.Hide;
        // 判断是否全部勾选
        var AllChecked = conf.SetupLaunch && conf.SetupUi && conf.SetupJava && conf.SetupUpdate && conf.SetupGameLink &&
                         conf.SetupAbout && conf.SetupFeedback && conf.SetupLog && conf.SetupLauncherMisc &&
                         conf.SetupGameManage;
        CheckHiddenPageSetup.Checked = AllChecked;
    }

    // ================= 工具页面协同 =================
    private void HiddenToolsMain(object sender, bool user)
    {
        if (!user)
            return;
        var IsChecked = (bool)CheckHiddenPageTools.Checked;
        CheckHiddenToolsGameLink.Checked = IsChecked;
        CheckHiddenToolsHelp.Checked = IsChecked;
        CheckHiddenToolsTest.Checked = IsChecked;
    }

    private void HiddenToolsSub(object sender, bool user)
    {
        if (!user)
            return;
        var conf = Config.Preference.Hide;
        var AllChecked = conf.ToolsGameLink && conf.ToolsHelp && conf.ToolsTest;
        CheckHiddenPageTools.Checked = AllChecked;
    }

    // 警告提示
    private void HiddenHint(object sender, bool user)
    {
        if (ModAnimation.AniControlEnabled == 0 && sender is MyCheckBox checkBox && checkBox.Checked == true)
            ModMain.Hint("按 F12 即可暂时关闭功能隐藏设置。千万别忘了，要不然设置就改不回来了……");
    }

    #endregion
}
