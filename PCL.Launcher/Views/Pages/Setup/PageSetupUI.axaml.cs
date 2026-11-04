using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;
using PCL.UI.Seams;
using PCL.Views.Pages.Launch;
using PCL.Views.Shell;

namespace PCL.Views.Pages.Setup;

/// <summary>
///     外观设置页：窗口透明度、深浅色模式、配色主题、字体、标题栏类型、背景、背景音乐、自定义主页与功能隐藏。
///     所有开关与下拉直接读写 <see cref="Config" />；配色变更由 <c>ThemeService</c> 订阅配置事件后生效，
///     语言与字体分别由 <c>LocalizationService</c> / 字体配置生效。
/// </summary>
public partial class PageSetupUI : MyPageRight, ISetupResetPage
{
    private static readonly (string Tag, string Key)[] ThemeColorKeys =
    [
        (nameof(ColorTheme.SkyBlue), "Setup.Ui.Theme.Color.SkyBlue"),
        (nameof(ColorTheme.CatBlue), "Setup.Ui.Theme.Color.CatBlue"),
        (nameof(ColorTheme.DeathBlue), "Setup.Ui.Theme.Color.CrashBlue"),
        (nameof(ColorTheme.HmclBlue), "Setup.Ui.Theme.Color.Hmcl")
    ];

    private readonly List<MyCheckBox> _hideChecks = [];
    private readonly MyRadioBox[] _homepageRadios;
    private readonly MyRadioBox[] _logoTypeRadios;
    private bool _loaded;
    private bool _reloading;

    public PageSetupUI()
    {
        InitializeComponent();
        PanScroll = PanBack;

        _logoTypeRadios = [RadioLogoType0, RadioLogoType1, RadioLogoType2, RadioLogoType3];
        _homepageRadios = [RadioCustomType0, RadioCustomType3, RadioCustomType1, RadioCustomType2];

        SliderLauncherOpacity.getHintText = new Func<object, object>(FormatLauncherOpacityHint);
        SliderBackgroundOpacity.getHintText = new Func<object, object>(FormatBackgroundOpacityHint);
        SliderBackgroundBlur.getHintText = new Func<object, object>(FormatPixelHint);
        SliderBlurValue.getHintText = new Func<object, object>(FormatPixelHint);
        SliderBlurSamplingRate.getHintText = new Func<object, object>(FormatSamplingRateHint);
        SliderMusicVolume.getHintText = new Func<object, object>(FormatMusicVolumeHint);

        SliderLauncherOpacity.Change += (_, _) =>
            WriteIfReady(() => Config.Preference.Theme.WindowOpacity = SliderLauncherOpacity.Value);
        SliderBackgroundOpacity.Change += (_, _) => WriteIfReady(() =>
            Config.Preference.Background.WallpaperOpacity = SliderBackgroundOpacity.Value);
        SliderBackgroundBlur.Change += (_, _) => WriteIfReady(() =>
            Config.Preference.Background.WallpaperBlurRadius = SliderBackgroundBlur.Value);
        SliderBlurValue.Change += (_, _) =>
            WriteIfReady(() => Config.Preference.Blur.Radius = SliderBlurValue.Value);
        SliderBlurSamplingRate.Change += (_, _) =>
            WriteIfReady(() => Config.Preference.Blur.SamplingRate = SliderBlurSamplingRate.Value);
        SliderMusicVolume.Change += (_, _) =>
            WriteIfReady(() => Config.Preference.Music.Volume = SliderMusicVolume.Value);

        ComboDarkMode.SelectionChanged += (_, _) => WriteIfReady(() =>
        {
            if (ComboDarkMode.SelectedIndex >= 0)
                Config.Preference.Theme.ColorMode = (ColorMode)ComboDarkMode.SelectedIndex;
        });
        ComboLightColor.SelectionChanged += (_, _) => WriteIfReady(() =>
        {
            if (ComboLightColor.SelectedIndex >= 0)
                Config.Preference.Theme.LightColor = (ColorTheme)ComboLightColor.SelectedIndex;
        });
        ComboDarkColor.SelectionChanged += (_, _) => WriteIfReady(() =>
        {
            if (ComboDarkColor.SelectedIndex >= 0)
                Config.Preference.Theme.DarkColor = (ColorTheme)ComboDarkColor.SelectedIndex;
        });
        ComboBlurType.SelectionChanged += (_, _) => WriteIfReady(() =>
        {
            if (ComboBlurType.SelectedIndex >= 0)
                Config.Preference.Blur.KernelType = ComboBlurType.SelectedIndex;
        });
        ComboBackgroundSuit.SelectionChanged += (_, _) => WriteIfReady(() =>
        {
            if (ComboBackgroundSuit.SelectedIndex >= 0)
                Config.Preference.Background.WallpaperSuitMode = ComboBackgroundSuit.SelectedIndex;
        });

        CheckLauncherLogo.Change += (_, _) => WriteIfReady(() =>
            Config.Preference.ShowStartupLogo = CheckLauncherLogo.Checked == true);
        CheckLockWindowSize.Change += (_, _) => WriteIfReady(() =>
            Config.Preference.LockWindowSize = CheckLockWindowSize.Checked == true);
        CheckShowLaunchingHint.Change += (_, _) => WriteIfReady(() =>
            Config.Preference.ShowLaunchingHint = CheckShowLaunchingHint.Checked == true);
        CheckBackgroundColorful.Change += (_, _) => WriteIfReady(() =>
            Config.Preference.Background.BackgroundColorful =
                CheckBackgroundColorful.Checked == true);
        CheckAutoPauseVideo.Change += (_, _) => WriteIfReady(() =>
            Config.Preference.Background.AutoPauseVideo = CheckAutoPauseVideo.Checked == true);
        CheckLogoLeft.Change += (_, _) =>
            WriteIfReady(() => Config.Preference.TopBarLeftAlign = CheckLogoLeft.Checked == true);
        CheckBlur.Change += (_, _) => WriteIfReady(() =>
        {
            Config.Preference.Blur.IsEnabled = CheckBlur.Checked == true;
            PanBlurValue.IsVisible = CheckBlur.Checked == true;
        });

        CheckMusicRandom.Change += (_, _) => WriteIfReady(() =>
            Config.Preference.Music.ShufflePlayback = CheckMusicRandom.Checked == true);
        CheckMusicAuto.Change += (_, _) => WriteIfReady(() =>
            Config.Preference.Music.StartOnStartup = CheckMusicAuto.Checked == true);
        CheckMusicStart.Change += (_, _) => WriteIfReady(() =>
        {
            Config.Preference.Music.StartInGame = CheckMusicStart.Checked == true;
            if (CheckMusicStart.Checked == true && CheckMusicStop.Checked == true)
                CheckMusicStop.Checked = false;
        });
        CheckMusicStop.Change += (_, _) => WriteIfReady(() =>
        {
            Config.Preference.Music.StopInGame = CheckMusicStop.Checked == true;
            if (CheckMusicStop.Checked == true && CheckMusicStart.Checked == true)
                CheckMusicStart.Checked = false;
        });
        CheckMusicSMTC.Change += (_, _) =>
            WriteIfReady(() => Config.Preference.Music.EnableSMTC = CheckMusicSMTC.Checked == true);

        foreach (var (index, radio) in _logoTypeRadios.Index())
            radio.Check += (_, _) => WriteIfReady(() =>
            {
                if (!radio.Checked) return;
                Config.Preference.WindowTitleType = (LauncherTitleType)index;
                RefreshLogoPanels();
            });

        foreach (var (index, radio) in _homepageRadios.Index())
        {
            var type = index switch
            {
                0 => 0,
                1 => 3,
                2 => 1,
                _ => 2
            };
            radio.Check += (_, _) => WriteIfReady(() =>
            {
                if (!radio.Checked) return;
                Config.Preference.Homepage.Type = type;
                RefreshHomepagePanels();
            });
        }

        ComboUiFont.SelectionChanged += (_, _) =>
            WriteIfReady(() => Config.Preference.Font = ComboUiFont.SelectedFontTag);
        ComboUiMotdFont.SelectionChanged += (_, _) =>
            WriteIfReady(() => Config.Preference.MotdFont = ComboUiMotdFont.SelectedFontTag);
        TextLogoText.TextChanged += (_, _) => WriteIfReady(() =>
            Config.Preference.WindowTitleCustomText = TextLogoText.Text ?? string.Empty);
        TextCustomNet.TextChanged += (_, _) => WriteIfReady(() =>
            Config.Preference.Homepage.CustomUrl = TextCustomNet.Text ?? string.Empty);
        ComboCustomPreset.SelectionChanged += (_, _) => WriteIfReady(() =>
        {
            if (ComboCustomPreset.SelectedIndex >= 0)
                Config.Preference.Homepage.SelectedPreset = ComboCustomPreset.SelectedIndex;
        });

        BtnLauncherDonate.Click += (_, _) => Basics.OpenPath("https://afdian.com/a/LTCat");
        BtnLogoChange.Click += (_, _) => OnChangeLogo();
        BtnLogoDelete.Click += (_, _) => OnDeleteLogo();
        BtnBackgroundOpen.Click += (_, _) => OpenBackgroundFolder();
        BtnBackgroundRefresh.Click += (_, _) =>
        {
            BackgroundHost.RequestRefresh();
            RefreshBackgroundState();
            HintService.Show(Hints.Info(Lang.Text("Setup.Ui.Background.Refresh"), false));
        };
        BtnBackgroundClear.Click += async (_, _) => await ClearBackgroundAsync();
        BtnMusicOpen.Click += (_, _) => OpenFolder(MusicFolder);
        BtnMusicRefresh.Click += (_, _) =>
        {
            RefreshMusicState();
            HintService.Show(Hints.Info(Lang.Text("Music.Error.NotStarted"), false));
        };
        BtnMusicClear.Click += async (_, _) => await ClearMusicAsync();
        BtnCustomRefresh.Click += (_, _) =>
        {
            PageLaunchRight.Current?.ForceRefresh();
            HintService.Show(Hints.Success(Lang.Text("Setup.Ui.Homepage.Refresh.Success")));
        };
        BtnCustomTutorial.Click += (_, _) =>
            Basics.OpenPath("https://docs.pclc.cc/ce/customization/xaml-format");

        BuildHideGrid();
        BuildThemeColorItems();

        HiddenFeatureState.Changed += OnHiddenFeatureStateChanged;

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            Reload();
        };
    }

    public void Reset()
    {
        try
        {
            Config.Preference.Reset();
            Reload();
            HintService.Show(Hints.Success(Lang.Text("Setup.Ui.Initialized"), false));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Ui.Error.InitFailed"),
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Ui.Error.InitFailed"));
        }
    }

    public override void OnNavigatedTo(object? parameter)
    {
        RefreshBackgroundState();
        RefreshMusicState();
        Reload();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        if (!_loaded) return;
        _reloading = true;
        try
        {
            BuildThemeColorItems();
            BuildHideGrid();
        }
        finally
        {
            _reloading = false;
        }

        Reload();
        RefreshBackgroundState();
        RefreshMusicState();
    }

    public void Reload()
    {
        _reloading = true;
        try
        {
            var theme = Config.Preference.Theme;
            SliderLauncherOpacity.Value = theme.WindowOpacity;
            ComboDarkMode.SelectedIndex = Math.Clamp((int)theme.ColorMode, 0, 2);
            ComboLightColor.SelectedIndex = Math.Clamp(
                (int)theme.LightColor,
                0,
                Math.Max(0, ComboLightColor.ItemCount - 1));
            ComboDarkColor.SelectedIndex = Math.Clamp(
                (int)theme.DarkColor,
                0,
                Math.Max(0, ComboDarkColor.ItemCount - 1));
            CheckLauncherLogo.Checked = Config.Preference.ShowStartupLogo;
            CheckLockWindowSize.Checked = Config.Preference.LockWindowSize;
            CheckShowLaunchingHint.Checked = Config.Preference.ShowLaunchingHint;

            CheckBlur.Checked = Config.Preference.Blur.IsEnabled;
            SliderBlurValue.Value = Config.Preference.Blur.Radius;
            SliderBlurSamplingRate.Value = Config.Preference.Blur.SamplingRate;
            ComboBlurType.SelectedIndex = Math.Clamp(Config.Preference.Blur.KernelType, 0, 1);
            PanBlurValue.IsVisible = Config.Preference.Blur.IsEnabled;

            ComboUiFont.SelectedFontTag = Config.Preference.Font;
            ComboUiMotdFont.SelectedFontTag = Config.Preference.MotdFont;

            SliderBackgroundOpacity.Value = Config.Preference.Background.WallpaperOpacity;
            SliderBackgroundBlur.Value = Config.Preference.Background.WallpaperBlurRadius;
            ComboBackgroundSuit.SelectedIndex = Math.Clamp(
                Config.Preference.Background.WallpaperSuitMode,
                0,
                8);
            CheckBackgroundColorful.Checked = Config.Preference.Background.BackgroundColorful;
            CheckAutoPauseVideo.Checked = Config.Preference.Background.AutoPauseVideo;

            var titleType = (int)Config.Preference.WindowTitleType;
            if (titleType >= 0 && titleType < _logoTypeRadios.Length)
                _logoTypeRadios[titleType].SetChecked(true, false);
            TextLogoText.Text = Config.Preference.WindowTitleCustomText;
            CheckLogoLeft.Checked = Config.Preference.TopBarLeftAlign;
            RefreshLogoPanels();

            SliderMusicVolume.Value = Config.Preference.Music.Volume;
            CheckMusicRandom.Checked = Config.Preference.Music.ShufflePlayback;
            CheckMusicAuto.Checked = Config.Preference.Music.StartOnStartup;
            CheckMusicStart.Checked = Config.Preference.Music.StartInGame;
            CheckMusicStop.Checked = Config.Preference.Music.StopInGame;
            CheckMusicSMTC.Checked = Config.Preference.Music.EnableSMTC;

            var homepageType = Math.Clamp(Config.Preference.Homepage.Type, 0, 3);
            var homepageIndex = homepageType switch
            {
                0 => 0,
                3 => 1,
                1 => 2,
                _ => 3
            };
            _homepageRadios[homepageIndex].SetChecked(true, false);
            TextCustomNet.Text = Config.Preference.Homepage.CustomUrl;
            ComboCustomPreset.SelectedIndex = Math.Clamp(
                Config.Preference.Homepage.SelectedPreset,
                0,
                ComboCustomPreset.ItemCount - 1);
            RefreshHomepagePanels();

            RefreshFeatureHideState();
            ReloadHideChecks();
        }
        finally
        {
            _reloading = false;
        }

        RefreshBackgroundState();
        RefreshMusicState();
    }

    private void WriteIfReady(Action action)
    {
        if (_reloading) return;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, Lang.Text("Setup.Ui.Error.LoadFailed"));
        }
    }

    #region 主题色

    private void BuildThemeColorItems()
    {
        var count = Basics.IsAprilFool ? ThemeColorKeys.Length : ThemeColorKeys.Length - 1;
        foreach (var combo in new[]
                 {
                     ComboLightColor,
                     ComboDarkColor
                 })
        {
            combo.Items.Clear();
            for (var i = 0; i < count; i++)
                combo.Items.Add(
                    new MyComboBoxItem
                    {
                        Content = Lang.Text(ThemeColorKeys[i].Key)
                    });
        }
    }

    #endregion

    private static object FormatMusicVolumeHint(object value)
    {
        return Lang.Number(Math.Ceiling(Convert.ToDouble(value) * 0.1d) / 100d, "P0");
    }

    private static object FormatLauncherOpacityHint(object value)
    {
        return Lang.Number(Math.Round(40 + Convert.ToDouble(value) * 0.1d) / 100d, "P0");
    }

    private static object FormatBackgroundOpacityHint(object value)
    {
        return Lang.Number(Math.Round(Convert.ToDouble(value) * 0.1d) / 100d, "P0");
    }

    private static object FormatPixelHint(object value)
    {
        return Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(value), "N0"));
    }

    private static object FormatSamplingRateHint(object value)
    {
        return Lang.Number(Convert.ToDouble(value) / 100d, "P0");
    }

    #region 标题栏与主页

    private void RefreshLogoPanels()
    {
        var type = Config.Preference.WindowTitleType;
        CheckLogoLeft.IsVisible = type == LauncherTitleType.None;
        PanLogoText.IsVisible = type == LauncherTitleType.Text;
        PanLogoChange.IsVisible = type == LauncherTitleType.Image;
    }

    private void RefreshHomepagePanels()
    {
        var type = Config.Preference.Homepage.Type;
        PanCustomLocal.IsVisible = type == 1;
        PanCustomNet.IsVisible = type == 2;
        PanCustomPreset.IsVisible = type == 3;
    }

    #endregion

    #region 背景

    private static string BackgroundFolder => Path.Combine(Paths.Data, "Pictures");

    private void RefreshBackgroundState()
    {
        try
        {
            var count = CountFiles(BackgroundFolder);
            var show = count > 0;
            PanBackgroundSuit.IsVisible = show;
            PanBackgroundOpacity.IsVisible = show;
            PanBackgroundBlur.IsVisible = show;
            BtnBackgroundClear.IsVisible = show;
            // TODO(FIX-P3-05): 视频背景引擎（SEAM-3 / BackgroundHost）接入后恢复可勾选，并按实际能力决定显隐。
            CheckAutoPauseVideo.IsVisible = show;
            CheckAutoPauseVideo.IsEnabled = SeamCapabilities.MediaAvailable;
            if (!SeamCapabilities.MediaAvailable)
                ToolTip.SetTip(CheckAutoPauseVideo, Lang.Text("Tools.GameLink.Finish.Unavailable"));
            CardBackground.Title = show
                ? Lang.Text("Setup.Ui.Background.TitleWithCount", count)
                : Lang.Text("Setup.Ui.Background.TitleDefault");
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新背景状态失败");
        }
    }

    private static int CountFiles(string directory)
    {
        if (!Directory.Exists(directory)) return 0;
        try
        {
            return Directory.EnumerateFiles(directory).Count();
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static void OpenFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        Basics.OpenPath(folder);
    }

    private static void OpenBackgroundFolder()
    {
        OpenFolder(BackgroundFolder);
    }

    private async Task ClearBackgroundAsync()
    {
        var result = await DialogService.Instance.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Common.Dialog.Warning"),
                Content = Lang.Text("Setup.Ui.Background.Clear.Confirm.Message"),
                Buttons = DialogButtons.OkCancel,
                IsWarn = true
            });
        if (!result.IsConfirmed) return;
        try
        {
            if (Directory.Exists(BackgroundFolder))
                Directory.Delete(BackgroundFolder, true);
            Directory.CreateDirectory(BackgroundFolder);
            BackgroundHost.RequestRefresh();
            RefreshBackgroundState();
            HintService.Show(Hints.Success(Lang.Text("Setup.Ui.Background.Clear.Success")));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "清空背景内容失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    #endregion

    #region 背景音乐

    private static string MusicFolder => Path.Combine(Paths.Data, "Musics");

    private void RefreshMusicState()
    {
        try
        {
            var count = CountFiles(MusicFolder);
            var hasMusic = count > 0;
            PanMusicVolume.IsVisible = hasMusic;
            PanMusicDetail.IsVisible = hasMusic;
            BtnMusicClear.IsVisible = hasMusic;
            CardMusic.Title = hasMusic
                ? Lang.Text("Setup.Ui.Music.TitleWithCount", count)
                : Lang.Text("Setup.Ui.Music.Title");

            // TODO(FIX-P3-03): 播放引擎（SEAM-3）迁移后恢复刷新按钮并把提示换回 Setup.Ui.Music.Refresh.ToolTip；
            // 引擎缺失期间按钮禁用，配置项本身仍可正常读写。
            BtnMusicRefresh.IsEnabled = SeamCapabilities.MediaAvailable;
            ToolTip.SetTip(
                BtnMusicRefresh,
                SeamCapabilities.MediaAvailable
                    ? Lang.Text("Setup.Ui.Music.Refresh.ToolTip")
                    : Lang.Text("Tools.GameLink.Finish.Unavailable"));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新背景音乐状态失败");
        }
    }

    private async Task ClearMusicAsync()
    {
        var result = await DialogService.Instance.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Common.Dialog.Warning"),
                Content = Lang.Text("Setup.Ui.Music.Clear.Confirm.Message"),
                Buttons = DialogButtons.OkCancel,
                IsWarn = true
            });
        if (!result.IsConfirmed) return;
        try
        {
            HintService.Show(Hints.Info(Lang.Text("Setup.Ui.Music.Deleting")));
            if (Directory.Exists(MusicFolder))
                Directory.Delete(MusicFolder, true);
            Directory.CreateDirectory(MusicFolder);
            RefreshMusicState();
            HintService.Show(Hints.Success(Lang.Text("Setup.Ui.Music.Delete.Success")));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "清空背景音乐失败",
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    #endregion

    #region 标题栏图片

    private static string LogoPath => Path.Combine(Paths.Data, "Logo.png");

    private void OnChangeLogo()
    {
        var fileName = SystemDialogs.SelectFile(
            Lang.Text("Setup.Ui.ImageFile.Filter"),
            Lang.Text("Setup.Ui.ImageFile.SelectTitle"));
        if (string.IsNullOrEmpty(fileName)) return;
        try
        {
            File.Copy(fileName, LogoPath, true);
            HintService.Show(Hints.Success(Lang.Text("Setup.Ui.Logo.ChangeImage")));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "设置标题栏图片失败",
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    private void OnDeleteLogo()
    {
        try
        {
            if (File.Exists(LogoPath)) File.Delete(LogoPath);
            ((MyRadioBox)_logoTypeRadios[(int)LauncherTitleType.Default]).SetChecked(true, true);
            HintService.Show(Hints.Success(Lang.Text("Setup.Ui.Logo.Clear.Success")));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "清空标题栏图片失败",
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    #endregion

    #region 功能隐藏

    private void OnHiddenFeatureStateChanged()
    {
        RefreshFeatureHideState();
    }

    private void RefreshFeatureHideState()
    {
        try
        {
            var forceShow = HiddenFeatureState.ForceShow;
            var functionHidden = Config.Preference.Hide.FunctionHidden;
            CardSwitch.IsVisible = forceShow || !functionHidden;
            CardSwitch.Title = forceShow
                ? Lang.Text("Setup.Ui.FeatureHide.TitleTemporarilyDisabled")
                : Lang.Text("Setup.Ui.FeatureHide.Title");
        }
        catch (Exception)
        {
            CardSwitch.IsVisible = true;
        }
    }

    private void ReloadHideChecks()
    {
        foreach (var check in _hideChecks)
        {
            if (check.Tag is not string tag) continue;
            if (ConfigService.TryGetConfigItemNoType(tag, out var item)
                && item.GetValueNoType() is bool value)
                check.Checked = value;
        }
    }

    private void BuildHideGrid()
    {
        PanHideGrid.Children.Clear();
        _hideChecks.Clear();

        AddHideTitle(1, "Setup.Ui.FeatureHide.MainPage");
        AddHideTitle(2, "Setup.Ui.FeatureHide.SubSetup");
        AddHideTitle(4, "Setup.Ui.FeatureHide.SubTools");
        AddHideTitle(5, "Setup.Ui.FeatureHide.SubInstance");
        AddHideTitle(7, "Setup.Ui.FeatureHide.SpecificFeature");

        AddHideCheck(1, 2, "UiHiddenPageDownload", "Setup.Ui.FeatureHide.Item.Download");
        AddHideCheck(1, 3, "UiHiddenPageSetup", "Setup.Ui.FeatureHide.Item.Settings");
        AddHideCheck(1, 4, "UiHiddenPageTools", "Setup.Ui.FeatureHide.Item.Tools");

        AddHideCheck(2, 2, "UiHiddenSetupLaunch", "Setup.Ui.FeatureHide.Item.Launch");
        AddHideCheck(2, 3, "UiHiddenSetupJava", null, "Java");
        AddHideCheck(2, 4, "UiHiddenSetupGameManage", "Setup.Ui.FeatureHide.Item.GameManage");
        AddHideCheck(2, 5, "UiHiddenSetupGameLink", "Setup.Ui.FeatureHide.Item.GameLink");
        AddHideCheck(2, 6, "UiHiddenSetupUi", "Setup.Ui.FeatureHide.Item.Ui");

        AddHideCheck(3, 2, "UiHiddenSetupLauncherLanguage", "Setup.Ui.FeatureHide.Item.Language");
        AddHideCheck(3, 3, "UiHiddenSetupLauncherMisc", "Setup.Ui.FeatureHide.Item.Misc");
        AddHideCheck(3, 4, "UiHiddenSetupUpdate", "Setup.Ui.FeatureHide.Item.Update");
        AddHideCheck(3, 5, "UiHiddenSetupAbout", "Setup.Ui.FeatureHide.Item.About");
        AddHideCheck(3, 6, "UiHiddenSetupFeedback", "Setup.Ui.FeatureHide.Item.Feedback");
        AddHideCheck(3, 7, "UiHiddenSetupLog", "Setup.Ui.FeatureHide.Item.Log");

        AddHideCheck(4, 2, "UiHiddenToolsGameLink", "Setup.Ui.FeatureHide.Item.Lobby");
        AddHideCheck(4, 3, "UiHiddenToolsTest", "Setup.Ui.FeatureHide.Item.Toolbox");

        AddHideCheck(5, 2, "UiHiddenVersionEdit", "Setup.Ui.FeatureHide.Item.Edit");
        AddHideCheck(5, 3, "UiHiddenVersionExport", "Setup.Ui.FeatureHide.Item.Export");
        AddHideCheck(5, 4, "UiHiddenVersionSave", "Setup.Ui.FeatureHide.Item.Save");
        AddHideCheck(5, 5, "UiHiddenVersionScreenshot", "Setup.Ui.FeatureHide.Item.Screenshot");
        AddHideCheck(5, 6, "UiHiddenVersionMod", null, "Mod");

        AddHideCheck(6, 2, "UiHiddenVersionResourcePack", "Setup.Ui.FeatureHide.Item.ResourcePack");
        AddHideCheck(6, 3, "UiHiddenVersionShader", "Setup.Ui.FeatureHide.Item.Shader");
        AddHideCheck(
            6,
            4,
            "UiHiddenVersionSchematic",
            "Setup.Ui.FeatureHide.Item.Schematic",
            columnSpan: 2);
        AddHideCheck(6, 6, "UiHiddenVersionServer", "Setup.Ui.FeatureHide.Item.Server");

        AddHideCheck(
            7,
            2,
            "UiHiddenFunctionSelect",
            "Setup.Ui.FeatureHide.Item.InstanceSelect",
            tooltipKey: "Setup.Ui.FeatureHide.Item.InstanceSelect.ToolTip");
        AddHideCheck(
            7,
            3,
            "UiHiddenFunctionModUpdate",
            "Setup.Ui.FeatureHide.Item.ModUpdate",
            tooltipKey: "Setup.Ui.FeatureHide.Item.ModUpdate.ToolTip");
        AddHideCheck(
            7,
            4,
            "UiHiddenFunctionHidden",
            "Setup.Ui.FeatureHide.Item.FeatureHide",
            tooltipKey: "Setup.Ui.FeatureHide.Item.FeatureHide.ToolTip");

        _reloading = true;
        try
        {
            ReloadHideChecks();
        }
        finally
        {
            _reloading = false;
        }
    }

    private void AddHideTitle(int row, string langKey)
    {
        var text = new TextBlock
        {
            Text = Lang.Text(langKey),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(text, row);
        Grid.SetColumn(text, 0);
        PanHideGrid.Children.Add(text);
    }

    private void AddHideCheck(
        int row,
        int column,
        string configKey,
        string? langKey,
        string? literal = null,
        int columnSpan = 1,
        string? tooltipKey = null)
    {
        var check = new MyCheckBox
        {
            Height = 22d,
            VerticalAlignment = VerticalAlignment.Center,
            Text = literal ?? Lang.Text(langKey!),
            Tag = configKey
        };
        if (tooltipKey is not null)
            ToolTip.SetTip(check, Lang.Text(tooltipKey));
        check.Change += (_, _) =>
        {
            if (_reloading || check.Tag is not string tag) return;
            ConfigService.TrySetValue(tag, check.Checked == true);
            PageSetupLeft.NotifyHiddenSettingChanged();
            if (tag == "UiHiddenFunctionHidden")
                RefreshFeatureHideState();
        };
        Grid.SetRow(check, row);
        Grid.SetColumn(check, column);
        if (columnSpan > 1)
            Grid.SetColumnSpan(check, columnSpan);
        PanHideGrid.Children.Add(check);
        _hideChecks.Add(check);
    }

    #endregion
}