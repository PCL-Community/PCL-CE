using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI.Hints;
using PCL.Core.UI.Theme;
using PCL.Core.Utils.Exts;
using PCL.Navigation;
using PCL.Network;
using PCL.UI.Controls;
using PCL.UI.Seams;
using PCL.Views.Pages.Homepage;
using PCL.Views.Shell;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;
using static PCL.Animations;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     启动页左栏：实例状态、账号区与启动按钮。
///     实例与文件夹列表来自 <see cref="GameFolders" /> / <see cref="InstanceList" />，
///     启动、取消与启动中进度来自 <see cref="GameLaunch" />，账号来自 <see cref="ProfileService" />。
///     登录子视图按账号状态在 <c>PanLogin</c> 中切换，实例只创建一次并复用。
/// </summary>
public partial class PageLaunchLeft : MyPageLeft, ISeamLaunchPage, IShellShortcutTarget
{
    private readonly MyButton _btnCancel;
    private readonly MyButton _btnInstance;
    private readonly MyButton _btnLaunch;
    private readonly MyButton _btnMore;
    private readonly Grid _gridProgress;
    private readonly TextBlock _labLaunchingHint;
    private readonly TextBlock _labLaunchingName;
    private readonly TextBlock _labLaunchingProgress;
    private readonly TextBlock _labLaunchingStage;
    private readonly TextBlock _labVersion;
    private readonly MyLoading _loadLaunching;
    private readonly List<KeyValuePair<LaunchPageKind, Control>> _loginPages = [];
    private readonly Grid _panInput;
    private readonly Grid _panLaunch;
    private readonly Grid _panLaunching;
    private readonly Grid _panLaunchingHint;
    private readonly Grid _panLaunchingInfo;
    private readonly Grid _panLogin;
    private readonly Rectangle _progressFinished;
    private double _actualUsedHeight = double.NaN;
    private double _actualUsedWidth = double.NaN;

    private bool _isFirstLoad;
    private bool _isInfoHeightAnimating;

    private bool _isInfoWidthAnimating;
    private bool _isLoadFinished;
    private LaunchButtonAction _launchButtonAction = LaunchButtonAction.Loading;
    private string? _launchButtonLanguage;
    private ScaleTransform? _launchButtonScale;
    private int _launchButtonState = -1;
    private McInstance? _launchButtonVersion;
    private Loading.LoaderBase? _launchingStageLoader;
    private LaunchPageKind _loginPageCurrent = LaunchPageKind.None;
    private double _showProgress;

    public PageLaunchLeft()
    {
        InitializeComponent();
        Current = this;

        _panInput = this.FindControl<Grid>("PanInput")!;
        _panLaunch = this.FindControl<Grid>("PanLaunch")!;
        _panLaunching = this.FindControl<Grid>("PanLaunching")!;
        _panLogin = this.FindControl<Grid>("PanLogin")!;
        _panLaunchingInfo = this.FindControl<Grid>("PanLaunchingInfo")!;
        _panLaunchingHint = this.FindControl<Grid>("PanLaunchingHint")!;
        _btnLaunch = this.FindControl<MyButton>("BtnLaunch")!;
        _btnInstance = this.FindControl<MyButton>("BtnInstance")!;
        _btnMore = this.FindControl<MyButton>("BtnMore")!;
        _btnCancel = this.FindControl<MyButton>("BtnCancel")!;
        _labVersion = this.FindControl<TextBlock>("LabVersion")!;
        _labLaunchingName = this.FindControl<TextBlock>("LabLaunchingName")!;
        _labLaunchingStage = this.FindControl<TextBlock>("LabLaunchingStage")!;
        _labLaunchingProgress = this.FindControl<TextBlock>("LabLaunchingProgress")!;
        _labLaunchingHint = this.FindControl<TextBlock>("LabLaunchingHint")!;
        _gridProgress = this.FindControl<Grid>("GridLaunchProgress")!;
        _loadLaunching = this.FindControl<MyLoading>("LoadLaunching")!;
        _loadLaunching.Text = string.Empty;

        _progressFinished = this.FindControl<Rectangle>("ProgressFinished")!;
        RefreshProgressGradient();
        ActualThemeVariantChanged += (_, _) => RefreshProgressGradient();
        ThemeService.ColorModeChanged += (_, _) => RefreshProgressGradient();
        ThemeService.ColorThemeChanged += _ => RefreshProgressGradient();

        this.FindControl<Border>("HintBorder")!.Clip = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(0d, 0d, 1000d, 1000d)),
            new RectangleGeometry(new Rect(94d, 0d, 72d, 10d)));

        _labVersion.RenderTransformOrigin = new RelativePoint(0.5d, -0.2d, RelativeUnit.Relative);
        _labVersion.RenderTransform = new ScaleTransform(1d, 1d);
        AttachVersionScale();

        _panLaunchingInfo.SizeChanged += OnLaunchingInfoSizeChanged;

        _btnLaunch.Click += (_, _) => LaunchButtonClick();
        _btnInstance.Click += (_, _) => SelectInstance();
        _btnMore.Click += (_, _) => OpenInstanceSettings();
        _btnCancel.Click += (_, _) => CancelLaunch();

        BindLocalizedTexts();

        SeamHosts.LaunchPage = this;
        HiddenFeatureState.Changed += RefreshButtonsUI;
        ExtraButtonsPanel.AprilGiveupResetAction = ResetAprilState;
        FileDragHandler.AuthlibServerDropped += OnAuthlibServerDropped;

        Loaded += (_, _) =>
        {
            if (_isFirstLoad)
                RefreshPage(false);
            InitializePage();
            AttachVersionScale();
            ResetAprilState();
            RefreshButtonsUI();
            RefreshProgressGradient();
        };
    }

    public static PageLaunchLeft? Current { get; private set; }

    public bool IsShowingLaunching { get; private set; }

    public override void OnNavigatedTo(object? parameter)
    {
        InitializePage();
        RefreshButtonsUI();
    }

    public override void OnNavigatedFrom()
    {
    }

    private enum LaunchButtonAction
    {
        Loading,
        Launch,
        Download,
        Disabled
    }

    private enum LaunchPageKind
    {
        None,
        Auth,
        Ms,
        Profile,
        ProfileSkin,
        Offline
    }

    #region 本地化

    private void BindLocalizedTexts()
    {
        RefreshDynamicTexts();
        LocalizedText.Bind(
            this.FindControl<TextBlock>("LabLaunchingTitle")!,
            TextBlock.TextProperty,
            GetLaunchingTitleText);
        LocalizedText.Bind(
            this.FindControl<TextBlock>("LabLaunchingMethod")!,
            TextBlock.TextProperty,
            GetLaunchingMethodText);
    }

    private void RefreshDynamicTexts()
    {
        LocalizedText.Bind(_btnLaunch, MyButton.TextProperty, GetLaunchButtonText);
        LocalizedText.Bind(_labVersion, TextBlock.TextProperty, GetVersionText);
    }

    private string GetLaunchButtonText()
    {
        return _launchButtonAction switch
        {
            LaunchButtonAction.Loading => Lang.Text("Launch.Home.Button.Loading"),
            LaunchButtonAction.Download => Lang.Text("Launch.Home.Button.Download"),
            _ => Lang.Text("Launch.Home.Button.Launch")
        };
    }

    private string GetVersionText()
    {
        return _launchButtonState switch
        {
            0 => Lang.Text("Launch.Home.Instance.Loading"),
            1 or 2 => Lang.Text("Launch.Home.Instance.NotFound"),
            3 => InstanceList.McMcInstanceSelected?.Name ?? "",
            _ => ""
        };
    }

    private string GetLaunchingTitleText()
    {
        return IsShowingLaunching && _showProgress >= 1d
            ? Lang.Text("Launch.Status.Title.Launched")
            : GameLaunch.currentLaunchOptions?.SaveBatch is null
                ? Lang.Text("Launch.Status.Title.Launching")
                : Lang.Text("Launch.Status.Title.ExportingScript");
    }

    private string GetLaunchingMethodText()
    {
        var profile = ProfileService.Current;
        return profile?.ProfileType switch
        {
            ProfileType.Offline => Lang.Text("Launch.Account.Type.Offline"),
            ProfileType.Microsoft => Lang.Text("Launch.Account.Type.Microsoft"),
            ProfileType.Authlib or ProfileType.YggdrasilConnect =>
                Lang.Text("Launch.Account.Type.ThirdParty")
                + (string.IsNullOrEmpty(profile.ServerName) ? "" : " / " + profile.ServerName),
            _ => ""
        };
    }

    #endregion

    #region 初始化

    private void InitializePage()
    {
        if (_isFirstLoad)
            return;
        _isFirstLoad = true;

        InstanceList.mcInstanceListLoader.LoadingStateChanged += (_, _) =>
            Dispatcher.UIThread.Post(RefreshButtonsUI);
        GameFolders.McFolderListLoader.LoadingStateChanged += (_, _) =>
            Dispatcher.UIThread.Post(RefreshButtonsUI);
        LauncherState.InstanceSelectionChanged += _ => Dispatcher.UIThread.Post(RefreshButtonsUI);

        try
        {
            ProfileService.Load();
            if (ProfileService.LastUsedProfile >= 0
                && ProfileService.LastUsedProfile < ProfileService.Profiles.Count)
                ProfileService.SelectAt(ProfileService.LastUsedProfile);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "加载登录档案失败", LauncherLogLevel.Feedback);
        }

        RefreshPage(false);
        LauncherCommon.RunInNewThread(
            LoadInstanceAsync,
            "Instance Check",
            ThreadPriority.AboveNormal);
    }

    /// <summary>
    ///     后台确认游戏文件夹与当前实例，完成后在 UI 线程刷新按钮与账号区。
    /// </summary>
    private void LoadInstanceAsync()
    {
        McInstance? instance = null;
        try
        {
            // 自动整合包安装：先切换到启动器目录下的 .minecraft，再在确认文件夹后执行安装。
            var packInstallPath = ResolveAutoModpackPath();
            if (packInstallPath is not null)
                PrepareAutoModpackFolder();

            var folderPath = ResolveMinecraftFolder();

            if (packInstallPath is not null)
                InstallAutoModpack(packInstallPath);

            var selection = States.Game.SelectedInstance;
            if (!string.IsNullOrEmpty(selection))
            {
                var candidate = new McInstance(selection);
                if (candidate.PathInstance.StartsWithF(folderPath) && candidate.Check())
                    instance = candidate;
            }

            if (instance is null)
            {
                LoadInstanceList(folderPath);
                instance = PickFirstUsableInstance();
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "加载 Minecraft 实例失败", LauncherLogLevel.Feedback);
        }

        LauncherCommon.RunInUi(() =>
        {
            InstanceList.McMcInstanceSelected = instance;
            States.Game.SelectedInstance = instance?.Name ?? "";
            _isLoadFinished = true;
            RefreshButtonsUI();
            RefreshPage(false);
        });
    }

    private static string? ResolveAutoModpackPath()
    {
        var executableDirectory = LauncherPaths.ExecutableDirectory;
        var zipPath = Path.Combine(executableDirectory, "modpack.zip");
        var mrpackPath = Path.Combine(executableDirectory, "modpack.mrpack");
        string? installPath = null;
        if (File.Exists(zipPath))
            installPath = zipPath;
        if (File.Exists(mrpackPath))
            installPath = mrpackPath;
        if (installPath is not null && !RestrictedFeatures.Check("auto-modpack-install"))
            installPath = null;
        return installPath;
    }

    private static void PrepareAutoModpackFolder()
    {
        var separator = Path.DirectorySeparatorChar;
        var folder = Path.Combine(LauncherPaths.ExecutableDirectory, ".minecraft") + separator;
        States.Game.SelectedFolder = "$.minecraft" + separator;
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(Path.Combine(folder, "versions"));
            GameFolders.McFolderLauncherProfilesJsonCreate(folder);
        }

        var displayName = GetFolderDisplayName(folder);
        var folders = (States.Game.Folders ?? "")
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        if (!folders.Any(entry => entry.EndsWith(">" + folder, StringComparison.OrdinalIgnoreCase)))
            folders.Add($"{displayName}>{folder}");
        States.Game.Folders = string.Join("|", folders);
        GameFolders.mcFolderSelected = folder;
    }

    private static string GetFolderDisplayName(string folder)
    {
        var separator = Path.DirectorySeparatorChar;
        var trimmed = folder.TrimEnd(separator);
        var name = Path.GetFileName(trimmed) ?? "";
        if (name == ".minecraft")
            name = Path.GetFileName(Path.GetDirectoryName(trimmed) ?? "") ?? "";
        if (string.IsNullOrWhiteSpace(name))
            name = "Minecraft";
        return name.Length > 40 ? name[..39] : name;
    }

    private static void InstallAutoModpack(string installPath)
    {
        try
        {
            LauncherCommon.Log("[Launch] 需自动安装整合包：" + installPath);
            var installLoader = Modpack.ModpackInstall(installPath);
            installLoader.WaitForExit();
            if (installLoader.State == LoadState.Finished && File.Exists(installPath))
            {
                LauncherCommon.Log("[Launch] 自动安装整合包成功，清理安装包：" + installPath);
                File.Delete(installPath);
            }
        }
        catch (CancelledException ex)
        {
            LauncherCommon.Log(ex, "自动安装整合包被用户取消：" + installPath);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Select.Folder.Error.InstallPack", installPath),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Select.Folder.Error.InstallPack", installPath));
        }
    }

    private static string ResolveMinecraftFolder()
    {
        if (GameFolders.McFolderListLoader.State is LoadState.Waiting or LoadState.Failed)
            GameFolders.McFolderListLoader.Start(null, true);
        GameFolders.McFolderListLoader.WaitForExit();

        var selected = States.Game.SelectedFolder;
        var folderPath = string.IsNullOrEmpty(selected)
            ? ""
            : selected.Replace("$", LauncherPaths.ExecutableDirectory);
        if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
        {
            GameFolders.mcFolderSelected = folderPath;
            return folderPath;
        }

        LauncherCommon.Log(
            string.IsNullOrEmpty(folderPath)
                ? "[Launch] 没有已储存的 Minecraft 文件夹"
                : "[Launch] Minecraft 文件夹无效，该文件夹已不存在：" + folderPath,
            LauncherLogLevel.Debug);
        GameFolders.McFolderListLoader.Start(null, true);
        GameFolders.McFolderListLoader.WaitForExit();
        if (GameFolders.mcFolderList.Count == 0)
            return "";

        folderPath = GameFolders.mcFolderList[0].Location;
        States.Game.SelectedFolder = folderPath.Replace(LauncherPaths.ExecutableDirectory, "$");
        GameFolders.mcFolderSelected = folderPath;
        return folderPath;
    }

    private static void LoadInstanceList(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
            return;
        Loading.LoaderFolderRun(
            InstanceList.mcInstanceListLoader,
            folderPath,
            Loading.LoaderFolderRunType.ForceRun,
            1,
            "versions" + Path.DirectorySeparatorChar,
            true);
    }

    private static McInstance? PickFirstUsableInstance()
    {
        McInstance? first = null;
        foreach (var list in InstanceList.mcInstanceList.Values)
        foreach (var instance in list)
        {
            first ??= instance;
            break;
        }

        return first is null || first.Logo.Contains("RedstoneBlock") ? null : first;
    }

    #endregion

    #region 启动按钮与实例操作

    public void RefreshButtonsUI()
    {
        int currentState;
        if (!_isLoadFinished)
            currentState = 0;
        else if (InstanceList.McMcInstanceSelected is null)
            currentState = Config.Preference.Hide.PageDownload ? 1 : 2;
        else
            currentState = 3;

        var currentLanguage = LocalizationService.CurrentLanguage.Code;
        var currentPath = InstanceList.McMcInstanceSelected?.PathInstance ?? "";
        if (currentState != _launchButtonState
            || currentLanguage != _launchButtonLanguage
            || currentPath != (_launchButtonVersion?.PathInstance ?? ""))
        {
            _launchButtonLanguage = currentLanguage;
            _launchButtonVersion = InstanceList.McMcInstanceSelected;
            _launchButtonState = currentState;
            RefreshDynamicTexts();

            switch (currentState)
            {
                case 0:
                    _launchButtonAction = LaunchButtonAction.Loading;
                    _btnInstance.IsEnabled = false;
                    break;
                case 1:
                    _launchButtonAction = LaunchButtonAction.Disabled;
                    _btnInstance.IsEnabled = true;
                    break;
                case 2:
                    _launchButtonAction = LaunchButtonAction.Download;
                    _btnInstance.IsEnabled = true;
                    break;
                case 3:
                    _launchButtonAction = LaunchButtonAction.Launch;
                    _btnInstance.IsEnabled = true;
                    break;
            }
        }

        _btnLaunch.IsEnabled = currentState switch
        {
            0 or 1 => false,
            2 => true,
            _ => ProfileService.Current is not null
        };
        var instanceButtonVisible =
            !Config.Preference.Hide.FunctionSelect || HiddenFeatureState.ForceShow;
        _btnInstance.IsVisible = instanceButtonVisible;
        _btnMore.IsVisible = currentState == 3 && instanceButtonVisible;
    }

    public void LaunchButtonClick()
    {
        if (GameLaunch.mcLaunchLoader.State == LoadState.Loading || !_btnLaunch.IsEnabled)
            return;

        switch (_launchButtonAction)
        {
            case LaunchButtonAction.Launch:
                if (InstanceList.McMcInstanceSelected is null)
                    return;
                if (File.Exists(InstanceList.McMcInstanceSelected.PathInstance + ".pclignore"))
                {
                    HintService.Show(
                        Hints.Error(Lang.Text("Launch.Home.Instance.InstallingCannotLaunch")));
                    return;
                }

                GameLaunch.McLaunchStart();
                break;
            case LaunchButtonAction.Download:
                PageRouter.Navigate(PageType.Download, PageSubType.DownloadInstall);
                break;
            case LaunchButtonAction.Loading:
            case LaunchButtonAction.Disabled:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void SelectInstance()
    {
        if (GameLaunch.mcLaunchLoader.State == LoadState.Loading)
            return;
        PageRouter.Navigate(PageType.InstanceSelect);
    }

    private static void OpenInstanceSettings()
    {
        if (GameLaunch.mcLaunchLoader.State == LoadState.Loading)
            return;
        var instance = InstanceList.McMcInstanceSelected;
        if (instance is null)
            return;
        if (File.Exists(instance.PathInstance + ".pclignore"))
        {
            HintService.Show(Hints.Error(Lang.Text("Launch.Home.Instance.InstallingCannotSetup")));
            return;
        }

        PageRouter.Navigate(PageType.InstanceSetup, PageSubType.Default, instance);
    }

    private void CancelLaunch()
    {
        if (GameLaunch.mcLaunchLoaderReal is null)
            return;
        GameLaunch.mcLaunchLoaderReal.Abort();
        GameLaunch.McLaunchLog("已取消启动");
        try
        {
            var process = GameLaunch.mcLaunchProcess ?? GameLaunch.mcLaunchWatcher?.gameProcess;
            if (process is { HasExited: false })
                process.Kill();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Minecraft.Launch.Error.CancelProcess"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Minecraft.Launch.Error.CancelProcess"));
        }
    }

    #endregion

    #region 启动中界面细节

    private void RefreshProgressGradient()
    {
        var start = ResolveColor("ColorObject4", Color.FromArgb(0xFF, 0x0, 0x0, 0x0));
        var end = ResolveColor("ColorObject3", Color.FromArgb(0xFF, 0x0, 0x0, 0x0));
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1d, 0d, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(new GradientStop(start, 0d));
        brush.GradientStops.Add(new GradientStop(end, 0.6d));
        _progressFinished.Fill = brush;
    }

    private Color ResolveColor(string key, Color fallback)
    {
        return this.TryFindResource(key, out var value) && value is Color color ? color : fallback;
    }

    /// <summary>
    ///     让版本号跟随启动按钮内层控件的真实缩放，包含按下、松开与悬停动画。
    /// </summary>
    private void AttachVersionScale()
    {
        if (_btnLaunch.RealRenderTransform is not ScaleTransform scale)
            return;
        if (ReferenceEquals(scale, _launchButtonScale))
            return;

        if (_launchButtonScale is not null)
            _launchButtonScale.PropertyChanged -= OnLaunchButtonScaleChanged;
        _launchButtonScale = scale;
        _launchButtonScale.PropertyChanged += OnLaunchButtonScaleChanged;
        ApplyVersionScale();
    }

    private void OnLaunchButtonScaleChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScaleTransform.ScaleXProperty
            || e.Property == ScaleTransform.ScaleYProperty)
            ApplyVersionScale();
    }

    private void ApplyVersionScale()
    {
        if (_labVersion.RenderTransform is not ScaleTransform scale)
            return;
        scale.ScaleX = _launchButtonScale?.ScaleX ?? 1d;
        scale.ScaleY = _launchButtonScale?.ScaleY ?? 1d;
    }

    private void OnLaunchingInfoSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var deltaWidth = e.NewSize.Width - e.PreviousSize.Width;
        if (e.PreviousSize.Width > 0d
            && !_isInfoWidthAnimating
            && Math.Abs(deltaWidth) >= 1d
            && _panLaunchingInfo.Bounds.Width > 0d)
        {
            _isInfoWidthAnimating = true;
            _actualUsedWidth = _panLaunchingInfo.Width;
            var previousWidth = e.PreviousSize.Width;
            AniStart(
                new[]
                {
                    AaWidth(_panLaunchingInfo, deltaWidth, 180, ease: new AniEaseOutFluent()),
                    AaCode(
                        () =>
                        {
                            _isInfoWidthAnimating = false;
                            _panLaunchingInfo.Width = _actualUsedWidth;
                        },
                        after: true)
                },
                "Launching Info Width");
            _panLaunchingInfo.Width = previousWidth;
        }

        var deltaHeight = e.NewSize.Height - e.PreviousSize.Height;
        if (e.PreviousSize.Height > 0d
            && !_isInfoHeightAnimating
            && Math.Abs(deltaHeight) >= 1d
            && _panLaunchingInfo.Bounds.Height > 0d)
        {
            _isInfoHeightAnimating = true;
            _actualUsedHeight = _panLaunchingInfo.Height;
            var previousHeight = e.PreviousSize.Height;
            AniStart(
                new[]
                {
                    AaHeight(_panLaunchingInfo, deltaHeight, 180, ease: new AniEaseOutFluent()),
                    AaCode(
                        () =>
                        {
                            _isInfoHeightAnimating = false;
                            _panLaunchingInfo.Height = _actualUsedHeight;
                        },
                        after: true)
                },
                "Launching Info Height");
            _panLaunchingInfo.Height = previousHeight;
        }
    }

    #endregion

    #region 启动中页面

    public void PageChangeToLaunching()
    {
        IsShowingLaunching = true;
        _showProgress = 0d;
        _launchingStageLoader = null;
        _labLaunchingName.Text = InstanceList.McMcInstanceSelected?.Name ?? "";
        _labLaunchingStage.Text = Lang.Text("Common.Action.Initialize");
        _labLaunchingProgress.Text = Lang.Number(0d, "P2");
        _labLaunchingHint.Text = Config.Preference.ShowLaunchingHint
            ? HomepageHints.GetRandomHint(true, true)
            : "";
        _gridProgress.ColumnDefinitions[0].Width = new GridLength(0d, GridUnitType.Star);
        _gridProgress.ColumnDefinitions[1].Width = new GridLength(1d, GridUnitType.Star);
        _labLaunchingProgress.IsVisible = true;
        _labLaunchingProgress.Opacity = 1d;
        var progressLeft = this.FindControl<TextBlock>("LabLaunchingProgressLeft")!;
        progressLeft.IsVisible = true;
        progressLeft.Opacity = 0.6d;
        this.FindControl<TextBlock>("LabLaunchingDownload")!.Opacity = 0d;
        this.FindControl<TextBlock>("LabLaunchingDownloadLeft")!.Opacity = 0d;
        _panLaunchingHint.IsVisible = false;
        _panLaunchingHint.Opacity = 0d;
        _panLaunchingInfo.Width = double.NaN;
        _panLaunchingInfo.Height = double.NaN;
        GameLaunch.mcLaunchProcess = null;
        GameLaunch.mcLaunchWatcher = null;

        LocalizedText.Bind(
            this.FindControl<TextBlock>("LabLaunchingTitle")!,
            TextBlock.TextProperty,
            GetLaunchingTitleText);
        LocalizedText.Bind(
            this.FindControl<TextBlock>("LabLaunchingMethod")!,
            TextBlock.TextProperty,
            GetLaunchingMethodText);

        _panInput.IsHitTestVisible = false;
        _panLaunching.IsHitTestVisible = false;
        _loadLaunching.Text = string.Empty;
        _loadLaunching.State.LoadingState = LoadingState.Run;
        _panLaunching.IsVisible = true;

        AniStart(
            new List<AniData>
            {
                AaOpacity(_panInput, -_panInput.Opacity, 110, ease: new AniEaseInFluent()),
                AaScaleTransform(_panInput, 1.2d - GetScale(_panInput).ScaleX, 160),
                AaOpacity(_panLaunching, 1d - _panLaunching.Opacity, 150, 100),
                AaScaleTransform(
                    _panLaunching,
                    1d - GetScale(_panLaunching).ScaleX,
                    500,
                    100,
                    new AniEaseOutBack(AniEasePower.Weak)),
                AaCode(() => _panLaunching.IsHitTestVisible = true, 150)
            },
            "Launch State Page");
    }

    public void PageChangeToLogin()
    {
        IsShowingLaunching = false;
        if (FindLoginPage(_loginPageCurrent) is null)
            RefreshPage(false);
        if (FindLoginPage(_loginPageCurrent) is ILoginPage loginPage)
            loginPage.Reload();
        _panInput.IsHitTestVisible = false;
        _panLaunching.IsHitTestVisible = false;
        _loadLaunching.State.LoadingState = LoadingState.Stop;
        _panInput.IsVisible = true;

        AniStart(
            new List<AniData>
            {
                AaOpacity(_panLaunching, -_panLaunching.Opacity, 150),
                AaScaleTransform(
                    _panLaunching,
                    0.8d - GetScale(_panLaunching).ScaleX,
                    150,
                    ease: new AniEaseOutFluent(AniEasePower.Weak)),
                AaOpacity(_panInput, 1d - _panInput.Opacity, 250, 50),
                AaScaleTransform(
                    _panInput,
                    1d - GetScale(_panInput).ScaleX,
                    300,
                    50,
                    new AniEaseOutBack(AniEasePower.Weak)),
                AaCode(() => _panInput.IsHitTestVisible = true, 200)
            },
            "Launch State Page",
            true);
    }

    public void LaunchingRefresh()
    {
        if (!LauncherCommon.RunInUi())
        {
            LauncherCommon.RunInUi(LaunchingRefresh);
            return;
        }

        if (!IsShowingLaunching)
            return;
        var launchLoader = GameLaunch.mcLaunchLoaderReal;
        if (launchLoader is null || launchLoader.State == LoadState.Aborted)
            return;

        try
        {
            var isLaunched = false;
            foreach (var loader in launchLoader.GetLoaderList(false))
                if (loader.State is LoadState.Loading or LoadState.Waiting)
                {
                    if (!ReferenceEquals(_launchingStageLoader, loader))
                    {
                        _launchingStageLoader = loader;
                        var stageLoader = loader;
                        LocalizedText.Bind(
                            _labLaunchingStage,
                            TextBlock.TextProperty,
                            () => stageLoader.DisplayName);
                    }

                    isLaunched = loader.Identifier is
                        GameLaunch.LaunchStageWaitWindowId or GameLaunch.LaunchStageEndId;
                    break;
                }

            if (AniIsRun("Launch State Page"))
                isLaunched = false;

            var actualProgress = launchLoader.Progress;
            if (actualProgress >= _showProgress)
                _showProgress += (actualProgress - _showProgress) * 0.2d + 0.005d;
            if (actualProgress <= _showProgress)
                _showProgress = actualProgress;
            if (isLaunched)
                _showProgress = 1d;

            _labLaunchingProgress.Text = Lang.Number(_showProgress, "P2");
            _labLaunchingProgress.IsVisible = !isLaunched;
            this.FindControl<TextBlock>("LabLaunchingProgressLeft")!.IsVisible = !isLaunched;

            var hasLaunchDownloader = false;
            try
            {
                foreach (var task in NetFacade.NetManager.Tasks)
                    if (task.RealParent?.Identifier == GameLaunch.LaunchStageRootId
                        && task.State == LoadState.Loading)
                        hasLaunchDownloader = true;
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "获取 Minecraft 启动下载器失败，可能是因为启动被取消");
            }

            var downloadText = this.FindControl<TextBlock>("LabLaunchingDownload")!;
            var downloadLeft = this.FindControl<TextBlock>("LabLaunchingDownloadLeft")!;
            downloadText.Text = LauncherCommon.GetString(NetFacade.NetManager.Speed) + "/s";
            var downloadOpacity = hasLaunchDownloader ? 1d : 0d;
            if (Math.Abs(downloadText.Opacity - downloadOpacity) > 0.01d)
                AniStart(
                    new List<AniData>
                    {
                        AaOpacity(downloadText, downloadOpacity - downloadText.Opacity, 100),
                        AaOpacity(
                            downloadLeft,
                            (hasLaunchDownloader ? 0.5d : 0d) - downloadLeft.Opacity,
                            100)
                    },
                    "Launching Download");

            var shouldShowHint = isLaunched && Config.Preference.ShowLaunchingHint;
            if (shouldShowHint != _panLaunchingHint.IsVisible)
            {
                if (shouldShowHint)
                {
                    _labLaunchingHint.Text = HomepageHints.GetRandomHint(true, true);
                    _panLaunchingHint.IsVisible = true;
                }

                AniStart(
                    AaOpacity(
                        _panLaunchingHint,
                        (shouldShowHint ? 1d : 0d) - _panLaunchingHint.Opacity,
                        100),
                    "Launching Hint");
                if (!shouldShowHint)
                    _panLaunchingHint.IsVisible = false;
            }

            var columns = _gridProgress.ColumnDefinitions;
            AniStart(
                new List<AniData>
                {
                    AaGridLengthWidth(
                        columns[0],
                        _showProgress - columns[0].Width.Value,
                        260,
                        ease: new AniEaseOutFluent()),
                    AaGridLengthWidth(
                        columns[1],
                        1d - _showProgress - columns[1].Width.Value,
                        260,
                        ease: new AniEaseOutFluent())
                },
                "Launching Progress");
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Minecraft.Launch.Error.RefreshInfo"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Launch.Error.RefreshInfo"));
        }
    }

    private static ScaleTransform GetScale(Visual visual)
    {
        if (visual.RenderTransform is ScaleTransform scale)
            return scale;
        scale = new ScaleTransform();
        visual.RenderTransform = scale;
        return scale;
    }

    #endregion

    #region 登录页面切换

    public void RefreshPage(bool anim, GameLaunch.McLoginType targetLoginType = default)
    {
        LaunchPageKind kind;
        if (targetLoginType != default)
        {
            kind = targetLoginType switch
            {
                GameLaunch.McLoginType.Ms => LaunchPageKind.Ms,
                GameLaunch.McLoginType.Auth => LaunchPageKind.Auth,
                GameLaunch.McLoginType.Legacy => LaunchPageKind.Offline,
                _ => LaunchPageKind.Profile
            };
        }
        else if (ProfileService.Current is not null)
        {
            kind = LaunchPageKind.ProfileSkin;
            if (_launchButtonAction == LaunchButtonAction.Launch)
                _btnLaunch.IsEnabled = true;
        }
        else
        {
            kind = LaunchPageKind.Profile;
            if (_launchButtonAction != LaunchButtonAction.Download)
                _btnLaunch.IsEnabled = false;
        }

        if (_loginPageCurrent == kind)
        {
            (FindLoginPage(kind) as ILoginPage)?.Reload();
            return;
        }

        PageChange(kind, anim);
    }

    private Control GetLoginPage(LaunchPageKind kind)
    {
        foreach (var pair in _loginPages)
            if (pair.Key == kind)
                return pair.Value;

        Control page = kind switch
        {
            LaunchPageKind.Auth => new PageLoginAuth(),
            LaunchPageKind.Ms => new PageLoginMs(),
            LaunchPageKind.Profile => new PageLoginProfile(),
            LaunchPageKind.ProfileSkin => new PageLoginProfileSkin(),
            LaunchPageKind.Offline => new PageLoginOffline(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "登录分页种类无效")
        };

        if (page is ILoginPage_Bindable bindable)
        {
            bindable.BackRequested += () => RefreshPage(true);
            bindable.LoginTypeRequested += type => RefreshPage(true, type);
            bindable.RefreshRequested += () =>
            {
                RefreshPage(true);
                RefreshButtonsUI();
            };
        }

        _loginPages.Add(new KeyValuePair<LaunchPageKind, Control>(kind, page));
        return page;
    }

    private Control? FindLoginPage(LaunchPageKind kind)
    {
        foreach (var pair in _loginPages)
            if (pair.Key == kind)
                return pair.Value;
        return null;
    }

    private void PageChange(LaunchPageKind kind, bool anim)
    {
        try
        {
            AniStop("FrmLogin PageChange");
            var pageNew = GetLoginPage(kind);

            if (!anim)
            {
                SwapLoginPage(pageNew);
                _loginPageCurrent = kind;
                return;
            }

            AniStart(
                new List<AniData>
                {
                    AaOpacity(_panLogin, -_panLogin.Opacity, 100, ease: new AniEaseOutFluent()),
                    AaCode(() => SwapLoginPage(pageNew), 100),
                    AaOpacity(_panLogin, 1d - _panLogin.Opacity, 100, 120, new AniEaseInFluent())
                },
                "FrmLogin PageChange");
            _loginPageCurrent = kind;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text(
                    "Launch.Account.Error.SwitchPage",
                    LauncherCommon.GetStringFromEnum(kind) ?? ""),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text(
                    "Launch.Account.Error.SwitchPage",
                    LauncherCommon.GetStringFromEnum(kind) ?? ""));
        }
    }

    private void SwapLoginPage(Control pageNew)
    {
        AniBeginControl();
        _panLogin.Opacity = 1d;
        _panLogin.Children.Clear();
        _panLogin.Children.Add(pageNew);
        (pageNew as ILoginPage)?.Reload();
        AniEndControl();
    }

    #endregion

    #region 壳与业务 seam 接口

    /// <inheritdoc />
    public void RefreshLaunchButtons()
    {
        RefreshButtonsUI();
    }

    /// <inheritdoc />
    public void RefreshLaunchPage(bool show)
    {
        RefreshButtonsUI();
        RefreshPage(show);
    }

    /// <inheritdoc />
    public void ShowLogin()
    {
        PageChangeToLogin();
    }

    /// <inheritdoc />
    public void ShowLaunching()
    {
        PageChangeToLaunching();
    }

    /// <inheritdoc />
    public void RefreshLaunching()
    {
        LaunchingRefresh();
    }

    /// <inheritdoc />
    public bool ToggleHiddenInstances()
    {
        return false;
    }

    /// <inheritdoc />
    public bool LaunchGame()
    {
        if (!_btnLaunch.IsEnabled)
            return false;
        LaunchButtonClick();
        return true;
    }

    private void ResetAprilState()
    {
        if (_panLaunch.RenderTransform is not TransformGroup group)
            return;
        foreach (var transform in group.Children)
            switch (transform)
            {
                case ScaleTransform scale:
                    scale.ScaleX = 1d;
                    scale.ScaleY = 1d;
                    break;
                case TranslateTransform translate:
                    translate.X = 0d;
                    translate.Y = 0d;
                    break;
            }
    }

    private void OnAuthlibServerDropped(string server)
    {
        if (string.IsNullOrWhiteSpace(server))
            return;
        ProfileService.IsCreatingProfile = true;
        PageLoginAuth.PendingAuthServer = server;
        _loginPageCurrent = LaunchPageKind.None;
        RefreshPage(true, GameLaunch.McLoginType.Auth);
    }

    #endregion
}