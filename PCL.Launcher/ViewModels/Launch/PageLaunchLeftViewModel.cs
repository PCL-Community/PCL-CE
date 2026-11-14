using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Exts;
using PCL.Core.ViewModel;
using PCL.Navigation;
using PCL.Network;
using PCL.Views.Pages.Homepage;
using PCL.Views.Shell;

namespace PCL;

/// <summary>启动页左栏的视图模型：实例状态、账号状态与启动按钮状态机。</summary>
public partial class PageLaunchLeftViewModel : ViewModelBase
{
    /// <summary>启动页左栏内嵌的登录子视图种类。</summary>
    public enum LaunchPageKind
    {
        None,
        Auth,
        Ms,
        Profile,
        ProfileSkin,
        Offline
    }

    private bool _isLoadFinished;

    private LaunchButtonAction _launchButtonAction = LaunchButtonAction.Loading;
    private string? _launchButtonLanguage;
    private int _launchButtonState = -1;

    private McInstance? _launchButtonVersion;

    private Loading.LoaderBase? _launchingStageLoader;

    [ObservableProperty]
    private string _launchingTitleText = Lang.Text("Launch.Status.Title.Launching");

    [ObservableProperty] private bool _shouldShowHint;

    [ObservableProperty] private double _showProgress;

    [ObservableProperty] private string _versionNameText = string.Empty;

    public PageLaunchLeftViewModel()
    {
        HiddenFeatureState.Changed += () => Dispatcher.UIThread.Post(RefreshButtonState);
    }

    [ObservableProperty] public partial bool HasLaunchDownloader { get; set; }

    [ObservableProperty] public partial bool IsInstanceButtonEnabled { get; set; }

    [ObservableProperty] public partial bool IsInstanceButtonVisible { get; set; } = true;

    [ObservableProperty] public partial bool IsLaunchButtonEnabled { get; set; }

    [ObservableProperty] public partial bool IsLaunched { get; set; }

    [ObservableProperty] public partial bool IsLaunchingProgressVisible { get; set; } = true;

    [ObservableProperty] public partial bool IsMoreButtonVisible { get; set; }

    [ObservableProperty] public partial bool IsShowingLaunching { get; set; }

    [ObservableProperty]
    public partial string LaunchButtonText { get; set; } = Lang.Text("Launch.Home.Button.Loading");

    [ObservableProperty] public partial string LaunchingDownloadText { get; set; } = string.Empty;

    [ObservableProperty] public partial string LaunchingHintText { get; set; } = string.Empty;

    [ObservableProperty] public partial string LaunchingMethodText { get; set; } = string.Empty;

    [ObservableProperty] public partial string LaunchingNameText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LaunchingProgressText { get; set; } = Lang.Number(0d, "P2");

    [ObservableProperty]
    public partial string LaunchingStageText { get; set; } = Lang.Text("Common.Action.Initialize");

    public bool IsFirstLoadStarted { get; private set; }

    /// <summary>后台实例检查完成或需要刷新登录子视图时触发，由页面转发到自身刷新逻辑。</summary>
    public event Action? RefreshPageRequested;

    partial void OnShowProgressChanged(double value)
    {
        LaunchingProgressText = Lang.Number(value, "P2");
        RefreshLaunchingTitle();
    }

    /// <summary>首次进入启动页时加载实例与账号信息；重复调用只刷新按钮状态。</summary>
    public void InitializePage()
    {
        RefreshButtonState();
        if (IsFirstLoadStarted)
            return;
        IsFirstLoadStarted = true;

        InstanceList.mcInstanceListLoader.LoadingStateChanged += (_, _) =>
            Dispatcher.UIThread.Post(RefreshButtonState);
        GameFolders.McFolderListLoader.LoadingStateChanged += (_, _) =>
            Dispatcher.UIThread.Post(RefreshButtonState);
        LauncherState.InstanceSelectionChanged += _ =>
            Dispatcher.UIThread.Post(RefreshButtonState);

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

        RefreshPageRequested?.Invoke();
        LauncherCommon.RunInNewThread(
            LoadInstanceAsync,
            "Instance Check",
            ThreadPriority.AboveNormal);
    }

    /// <summary>在 UI 线程重新计算启动按钮与实例按钮的文案、可用性和可见性。</summary>
    public void RefreshButtonState()
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
            _launchButtonState = currentState;
            _launchButtonLanguage = currentLanguage;
            _launchButtonVersion = InstanceList.McMcInstanceSelected;
        }

        _launchButtonAction = currentState switch
        {
            0 => LaunchButtonAction.Loading,
            1 => LaunchButtonAction.Disabled,
            2 => LaunchButtonAction.Download,
            _ => LaunchButtonAction.Launch
        };

        LaunchButtonText = _launchButtonAction switch
        {
            LaunchButtonAction.Loading => Lang.Text("Launch.Home.Button.Loading"),
            LaunchButtonAction.Download => Lang.Text("Launch.Home.Button.Download"),
            _ => Lang.Text("Launch.Home.Button.Launch")
        };
        VersionNameText = currentState switch
        {
            0 => Lang.Text("Launch.Home.Instance.Loading"),
            1 or 2 => Lang.Text("Launch.Home.Instance.NotFound"),
            3 => InstanceList.McMcInstanceSelected?.Name ?? "",
            _ => ""
        };

        IsInstanceButtonEnabled = currentState != 0;
        IsLaunchButtonEnabled = currentState switch
        {
            0 or 1 => false,
            2 => true,
            _ => ProfileService.Current is not null
        };
        var instanceButtonVisible =
            !Config.Preference.Hide.FunctionSelect || HiddenFeatureState.ForceShow;
        IsInstanceButtonVisible = instanceButtonVisible;
        IsMoreButtonVisible = currentState == 3 && instanceButtonVisible;
    }

    /// <summary>刷新语言相关的按钮与启动中文本。</summary>
    public void RefreshLocalization()
    {
        RefreshButtonState();
        LaunchingStageText = _launchingStageLoader?.DisplayName
            ?? Lang.Text("Common.Action.Initialize");
        RefreshLaunchingMethod();
        RefreshLaunchingTitle();
    }

    /// <summary>根据当前账号与目标登录类型决定登录子视图。</summary>
    public LaunchPageKind ResolveLoginPageKind(GameLaunch.McLoginType targetLoginType = default)
    {
        if (targetLoginType != default)
            return targetLoginType switch
            {
                GameLaunch.McLoginType.Ms => LaunchPageKind.Ms,
                GameLaunch.McLoginType.Auth => LaunchPageKind.Auth,
                GameLaunch.McLoginType.Legacy => LaunchPageKind.Offline,
                _ => LaunchPageKind.Profile
            };

        if (ProfileService.Current is not null)
        {
            if (_launchButtonAction == LaunchButtonAction.Launch)
                IsLaunchButtonEnabled = true;
            return LaunchPageKind.ProfileSkin;
        }

        if (_launchButtonAction != LaunchButtonAction.Download)
            IsLaunchButtonEnabled = false;
        return LaunchPageKind.Profile;
    }

    /// <summary>进入启动中界面，重置进度状态。</summary>
    public void BeginLaunching()
    {
        IsShowingLaunching = true;
        ShowProgress = 0d;
        IsLaunched = false;
        HasLaunchDownloader = false;
        ShouldShowHint = false;
        IsLaunchingProgressVisible = true;
        _launchingStageLoader = null;
        LaunchingNameText = InstanceList.McMcInstanceSelected?.Name ?? "";
        LaunchingStageText = Lang.Text("Common.Action.Initialize");
        LaunchingHintText = Config.Preference.ShowLaunchingHint
            ? HomepageHints.GetRandomHint(true, true)
            : "";
        RefreshLaunchingMethod();
        RefreshLaunchingTitle();
        GameLaunch.mcLaunchProcess = null;
        GameLaunch.mcLaunchWatcher = null;
    }

    /// <summary>退出启动中界面。</summary>
    public void EndLaunching()
    {
        IsShowingLaunching = false;
    }

    /// <summary>
    ///     刷新启动进度、当前阶段、下载速度与提示状态；实际控件动画由页面根据结果执行。
    /// </summary>
    public bool RefreshLaunching(bool animationRunning)
    {
        if (!IsShowingLaunching)
            return false;
        var launchLoader = GameLaunch.mcLaunchLoaderReal;
        if (launchLoader is null || launchLoader.State == LoadState.Aborted)
            return false;

        try
        {
            var isLaunched = false;
            foreach (var loader in launchLoader.GetLoaderList(false))
                if (loader.State is LoadState.Loading or LoadState.Waiting)
                {
                    if (!ReferenceEquals(_launchingStageLoader, loader))
                    {
                        _launchingStageLoader = loader;
                        LaunchingStageText = loader.DisplayName;
                    }

                    isLaunched = loader.Identifier is
                        GameLaunch.LaunchStageWaitWindowId or GameLaunch.LaunchStageEndId;
                    break;
                }

            if (animationRunning)
                isLaunched = false;

            var actualProgress = launchLoader.Progress;
            if (actualProgress >= ShowProgress)
                ShowProgress += (actualProgress - ShowProgress) * 0.2d + 0.005d;
            if (actualProgress <= ShowProgress)
                ShowProgress = actualProgress;
            if (isLaunched)
                ShowProgress = 1d;

            IsLaunched = isLaunched;
            IsLaunchingProgressVisible = !isLaunched;

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

            HasLaunchDownloader = hasLaunchDownloader;
            LaunchingDownloadText = LauncherCommon.GetString(NetFacade.NetManager.Speed) + "/s";

            var shouldShowHint = isLaunched && Config.Preference.ShowLaunchingHint;
            if (shouldShowHint && !ShouldShowHint)
                LaunchingHintText = HomepageHints.GetRandomHint(true, true);
            ShouldShowHint = shouldShowHint;
            return true;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Minecraft.Launch.Error.RefreshInfo"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Launch.Error.RefreshInfo"));
            return false;
        }
    }

    [RelayCommand]
    private void Launch()
    {
        if (GameLaunch.mcLaunchLoader.State == LoadState.Loading || !IsLaunchButtonEnabled)
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

    [RelayCommand]
    private static void SelectInstance()
    {
        if (GameLaunch.mcLaunchLoader.State == LoadState.Loading)
            return;
        PageRouter.Navigate(PageType.InstanceSelect);
    }

    [RelayCommand]
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

    [RelayCommand]
    private static void CancelLaunch()
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

    private void RefreshLaunchingMethod()
    {
        var profile = ProfileService.Current;
        LaunchingMethodText = profile?.ProfileType switch
        {
            ProfileType.Offline => Lang.Text("Launch.Account.Type.Offline"),
            ProfileType.Microsoft => Lang.Text("Launch.Account.Type.Microsoft"),
            ProfileType.Authlib or ProfileType.YggdrasilConnect =>
                Lang.Text("Launch.Account.Type.ThirdParty")
                + (string.IsNullOrEmpty(profile.ServerName) ? "" : " / " + profile.ServerName),
            _ => ""
        };
    }

    private void RefreshLaunchingTitle()
    {
        LaunchingTitleText = IsShowingLaunching && ShowProgress >= 1d
            ? Lang.Text("Launch.Status.Title.Launched")
            : GameLaunch.currentLaunchOptions?.SaveBatch is null
                ? Lang.Text("Launch.Status.Title.Launching")
                : Lang.Text("Launch.Status.Title.ExportingScript");
    }

    private enum LaunchButtonAction
    {
        Loading,
        Launch,
        Download,
        Disabled
    }

    #region 实例加载

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
            RefreshButtonState();
            RefreshPageRequested?.Invoke();
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
}