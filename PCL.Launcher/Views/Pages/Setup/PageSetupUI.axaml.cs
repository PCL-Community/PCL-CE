using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.VisualTree;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Validate;
using PCL.UI.Controls;
using PCL.UI.Controls.Common;
using PCL.Views.Pages.Launch;
using PCL.Views.Shell;

namespace PCL.Views.Pages.Setup;

/// <summary>
///     外观设置页：窗口透明度、深浅色模式、配色主题、字体、标题栏类型、背景、背景音乐、自定义主页与功能隐藏。
///     状态与配置写入由 <see cref="PageSetupUIViewModel" /> 承担；页面负责动态控件构建、文件选择与标题栏刷新。
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
    private bool _selectingLogo;

    public PageSetupUI()
    {
        InitializeComponent();
        PanScroll = PanBack;
        DataContext = ViewModel;

        _logoTypeRadios = [RadioLogoType0, RadioLogoType1, RadioLogoType2, RadioLogoType3];
        _homepageRadios = [RadioCustomType0, RadioCustomType3, RadioCustomType1, RadioCustomType2];

        SliderLauncherOpacity.getHintText =
            new Func<object, object>(PageSetupUIViewModel.FormatLauncherOpacityHint);
        SliderBackgroundOpacity.getHintText =
            new Func<object, object>(PageSetupUIViewModel.FormatBackgroundOpacityHint);
        SliderBackgroundBlur.getHintText =
            new Func<object, object>(PageSetupUIViewModel.FormatPixelHint);
        SliderBlurValue.getHintText =
            new Func<object, object>(PageSetupUIViewModel.FormatPixelHint);
        SliderBlurSamplingRate.getHintText =
            new Func<object, object>(PageSetupUIViewModel.FormatSamplingRateHint);
        SliderMusicVolume.getHintText =
            new Func<object, object>(PageSetupUIViewModel.FormatMusicVolumeHint);

        TextCustomNet.ValidateRules.Add(
            new HttpValidator
            {
                AllowsNullOrEmpty = true
            });
        TextCustomNet.ValidatedTextChanged += (_, _) => WriteIfReady(() =>
        {
            Config.Preference.Homepage.CustomUrl = TextCustomNet.Text ?? string.Empty;
            ViewModel.CustomNetText = TextCustomNet.Text ?? string.Empty;
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
        CheckLogoLeft.Change += (_, _) => WriteIfReady(() =>
            Config.Preference.TopBarLeftAlign = CheckLogoLeft.Checked == true);
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
        CheckMusicSMTC.Change += (_, _) => WriteIfReady(() =>
            Config.Preference.Music.EnableSMTC = CheckMusicSMTC.Checked == true);

        foreach (var (index, radio) in _logoTypeRadios.Index())
            radio.Check += (_, _) => WriteIfReady(() =>
            {
                if (!radio.Checked) return;
                Config.Preference.WindowTitleType = (LauncherTitleType)index;
                RefreshLogoPanels();
                RefreshTitleBar();
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
            if (MediaService.Current is not { IsMusicAvailable: true } media)
            {
                HintService.Show(Hints.Error(Lang.Text("Music.Error.OperationFailed")));
                RefreshMusicState();
                return;
            }

            media.RefreshMusic(true);
            RefreshMusicState();
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
            ViewModel.ShowLauncherHint();
        };
    }

    public PageSetupUIViewModel ViewModel { get; } = new();

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
        PanBack.ScrollToHome();
        if (_loaded) Reload();
        RefreshBackgroundState();
        RefreshMusicState();
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
            ViewModel.Reload(Math.Max(1, ComboLightColor.ItemCount));
            CheckLauncherLogo.Checked = Config.Preference.ShowStartupLogo;
            CheckLockWindowSize.Checked = Config.Preference.LockWindowSize;
            CheckShowLaunchingHint.Checked = Config.Preference.ShowLaunchingHint;

            CheckBlur.Checked = Config.Preference.Blur.IsEnabled;
            PanBlurValue.IsVisible = Config.Preference.Blur.IsEnabled;

            ComboUiFont.SelectedFontTag = Config.Preference.Font;
            ComboUiMotdFont.SelectedFontTag = Config.Preference.MotdFont;

            CheckBackgroundColorful.Checked = Config.Preference.Background.BackgroundColorful;

            var titleType = (int)Config.Preference.WindowTitleType;
            if (titleType >= 0 && titleType < _logoTypeRadios.Length)
                _logoTypeRadios[titleType].SetChecked(true, false);
            CheckLogoLeft.Checked = Config.Preference.TopBarLeftAlign;
            RefreshLogoPanels();

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

    private static void RefreshTitleBar()
    {
        var window = Application.Current?.ApplicationLifetime is
            IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        var titleBar = window?.GetVisualDescendants().OfType<TitleBar>().FirstOrDefault();
        if (titleBar is null) return;
        titleBar.RefreshTitleAppearance();
        if (Config.Preference.WindowTitleType == LauncherTitleType.Image
            && titleBar.FindControl<MyImage>("ImageTitleLogo") is { } image)
        {
            image.Source = null;
            image.Source = LogoPath;
        }
    }

    #endregion

    #region 背景

    private static string BackgroundFolder => Path.Combine(Paths.Data, "Pictures");

    private void RefreshBackgroundState()
    {
        try
        {
            var count = PageSetupUIViewModel.CountContentFiles(BackgroundFolder);
            var show = count > 0;
            ViewModel.ApplyBackgroundState(show, count);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新背景状态失败");
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
        ViewModel.BackgroundClearEnabled = false;
        try
        {
            if (!await ViewModel.ConfirmBackgroundClearAsync()) return;
            // 先释放当前图片句柄，避免 Windows 上文件仍被占用导致删除失败。
            BackgroundHost.Current?.Clear();
            await Task.Run(() =>
            {
                if (Directory.Exists(BackgroundFolder))
                    Directory.Delete(BackgroundFolder, true);
                Directory.CreateDirectory(BackgroundFolder);
            });
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
        finally
        {
            ViewModel.BackgroundClearEnabled = true;
        }
    }

    #endregion

    #region 背景音乐

    private static string MusicFolder => Path.Combine(Paths.Data, "Musics");

    private void RefreshMusicState()
    {
        try
        {
            var service = MediaService.Current;
            var count = service?.MusicCount
                ?? PageSetupUIViewModel.CountContentFiles(MusicFolder);
            var hasMusic = count > 0;
            var musicAvailable = !hasMusic || service?.IsMusicAvailable == true;
            ViewModel.ApplyMusicState(hasMusic, count);
            BtnMusicRefresh.IsEnabled = musicAvailable;
            ToolTip.SetTip(
                BtnMusicRefresh,
                musicAvailable
                    ? Lang.Text("Setup.Ui.Music.Refresh.ToolTip")
                    : Lang.Text("Tools.GameLink.Finish.Unavailable"));
            ToolTip.SetShowOnDisabled(BtnMusicRefresh, true);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新背景音乐状态失败");
        }
    }

    private async Task ClearMusicAsync()
    {
        ViewModel.MusicClearEnabled = false;
        try
        {
            if (!await ViewModel.ConfirmMusicClearAsync()) return;
            HintService.Show(Hints.Info(Lang.Text("Setup.Ui.Music.Deleting")));

            MediaService.Current?.ClearMusic();
            await Task.Run(() =>
            {
                if (Directory.Exists(MusicFolder))
                    Directory.Delete(MusicFolder, true);
                Directory.CreateDirectory(MusicFolder);
            });
            MediaService.Current?.RefreshMusic(false);
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
        finally
        {
            ViewModel.MusicClearEnabled = true;
        }
    }

    #endregion

    #region 标题栏图片

    private static string LogoPath => Path.Combine(Paths.Data, "Logo.png");

    private static void OnChangeLogo()
    {
        var fileName = SystemDialogs.SelectFile(
            Lang.Text("Setup.Ui.ImageFile.Filter"),
            Lang.Text("Setup.Ui.ImageFile.SelectTitle"));
        if (string.IsNullOrEmpty(fileName)) return;
        try
        {
            File.Copy(fileName, LogoPath, true);
            RefreshTitleBar();
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

    private void RadioLogoType3_PreviewCheck(object? sender, RouteEventArgs e)
    {
        if (_selectingLogo || !e.raiseByMouse || Animations.AniExitsAnyControl()) return;
        if (File.Exists(LogoPath))
        {
            RefreshTitleBar();
            return;
        }

        _selectingLogo = true;
        try
        {
            var fileName = SystemDialogs.SelectFile(
                Lang.Text("Setup.Ui.ImageFile.Filter"),
                Lang.Text("Setup.Ui.ImageFile.SelectTitle"));
            if (string.IsNullOrEmpty(fileName))
            {
                e.handled = true;
                return;
            }

            try
            {
                File.Copy(fileName, LogoPath, true);
                RefreshTitleBar();
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(
                    ex,
                    "复制标题栏图片失败",
                    LauncherLogLevel.Msgbox,
                    userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
                e.handled = true;
            }
        }
        finally
        {
            _selectingLogo = false;
        }
    }

    private void OnDeleteLogo()
    {
        try
        {
            if (File.Exists(LogoPath)) File.Delete(LogoPath);
            Config.Preference.WindowTitleType = LauncherTitleType.Default;
            RadioLogoType1.SetChecked(true, true);
            RefreshTitleBar();
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
            ViewModel.ApplyFeatureHideState(
                HiddenFeatureState.ForceShow,
                Config.Preference.Hide.FunctionHidden);
        }
        catch (Exception)
        {
            ViewModel.FeatureHideVisible = true;
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

        var previous = _reloading;
        _reloading = true;
        try
        {
            ReloadHideChecks();
        }
        finally
        {
            _reloading = previous;
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