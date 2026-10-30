using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

public partial class PageLaunchLeft
{
    private double actualUsedHeight;
    private double actualUsedWidth;
    private int btnLaunchState;
    private string _btnLaunchLanguage;
    private McInstance btnLaunchVersion;
    private bool isHeightAnimating;
    public interface ILoginPage { void Reload(); }

    private enum LaunchButtonAction
    {
        Loading,
        Launch,
        Download,
        Disabled
    }

    private LaunchButtonAction _launchButtonAction;

    private static string StageWaitWindow => Lang.Text("Minecraft.Launch.Stage.WaitWindow");
    private static string StageEnd => Lang.Text("Minecraft.Launch.Stage.End");
    private static string StageRoot => Lang.Text("Minecraft.Launch.Stage.Root");

    // 加载当前实例
    private bool isLoad;

    private bool isLoadFinished;

    // 尺寸改变动画
    private bool isWidthAnimating;
    private double showProgress;

    private readonly PageLaunchLeftViewModel _vm = new();

    public PageLaunchLeft()
    {
        InitializeComponent();
        Loaded += PageLaunchLeft_Loaded;
        WeakLanguageChanged.Add(this, OnLanguageChanged);
        // Handles
        BtnLaunch.Click += BtnLaunch_Click;
        BtnLaunch.Loaded += (_, _) => RefreshButtonsUI();
        PanLaunchingInfo.SizeChanged += PanLaunchingInfo_SizeChangedW;
        PanLaunchingInfo.SizeChanged += PanLaunchingInfo_SizeChangedH;
        DataContext = _vm;
    }

    public PageLaunchLeftViewModel ViewModel => _vm;

    private static void OnLanguageChanged(PageLaunchLeft page) => Common.RunInUi(page.RefreshButtonsUI);

    public void PageLaunchLeft_Loaded(object sender, RoutedEventArgs e)
    {
        if (isLoad)
            RefreshPage(false);

        AprilPosTrans.X = 0d;
        AprilPosTrans.Y = 0d;

        if (isLoad)
            return;
        isLoad = true;
        Animations.AniBeginControl();

        // 开始按钮
        InstanceList.mcInstanceListLoader.LoadingStateChanged += (_, _) => RefreshButtonsUI();
        GameFolders.McFolderListLoader.LoadingStateChanged += (_, _) => RefreshButtonsUI();
        RefreshButtonsUI();

        // 初始化档案
        ProfileService.Load();
        if (ProfileService.LastUsedProfile >= 0 && ProfileService.LastUsedProfile < ProfileService.Profiles.Count)
            ProfileService.SelectAt(ProfileService.LastUsedProfile);

        // 加载实例
        Common.RunInNewThread(() =>
        {
            // 自动整合包安装：准备
            string packInstallPath = null;
            if (File.Exists(Path.Combine(Common.ExePath, "modpack.zip")))
                packInstallPath = Path.Combine(Common.ExePath, "modpack.zip");
            if (File.Exists(Path.Combine(Common.ExePath, "modpack.mrpack")))
                packInstallPath = Path.Combine(Common.ExePath, "modpack.mrpack");
            if (packInstallPath is not null && !RestrictedFeaturePolicy.Check("auto-modpack-install"))
                packInstallPath = null;
            if (packInstallPath is not null)
            {
                Common.Log("[Launch] 需自动安装整合包：" + packInstallPath, Common.LogLevel.Debug);
                States.Game.SelectedFolder = @"$.minecraft\";
                if (!Directory.Exists(Common.ExePath + @".minecraft\"))
                {
                    Directory.CreateDirectory(Common.ExePath + @".minecraft\");
                    Directory.CreateDirectory(Common.ExePath + @".minecraft\versions\");
                    GameFolders.McFolderLauncherProfilesJsonCreate(Common.ExePath + @".minecraft\");
                }

                PageSelectLeft.AddFolder(Common.ExePath + @".minecraft\",
                    Common.GetFolderNameFromPath(Common.ExePath), false);
                GameFolders.McFolderListLoader.WaitForExit();
            }

            // 确认 Minecraft 文件夹存在
            GameFolders.mcFolderSelected =
                States.Game.SelectedFolder.ToString().Replace("$", Common.ExePath);
            if (string.IsNullOrEmpty(GameFolders.mcFolderSelected) || !Directory.Exists(GameFolders.mcFolderSelected))
            {
                // 无效的文件夹
                if (string.IsNullOrEmpty(GameFolders.mcFolderSelected))
                    Common.Log("[Launch] 没有已储存的 Minecraft 文件夹");
                else
                    Common.Log("[Launch] Minecraft 文件夹无效，该文件夹已不存在：" + GameFolders.mcFolderSelected,
                        Common.LogLevel.Debug);
                GameFolders.McFolderListLoader.WaitForExit(isForceRestart: true);
                States.Game.SelectedFolder = GameFolders.mcFolderList[0].Location.Replace(Common.ExePath, "$");
            }

            Common.Log("[Launch] Minecraft 文件夹：" + GameFolders.mcFolderSelected);
            if (Config.Debug.AddRandomDelay)
                Thread.Sleep(RandomUtils.NextInt(500, 3000));
            // 自动整合包安装
            if (packInstallPath is not null)
                try
                {
                    var installLoader = Modpack.ModpackInstall(packInstallPath);
                    Common.Log("[Launch] 自动安装整合包已开始：" + packInstallPath);
                    installLoader.WaitForExit();
                    if (installLoader.State == Common.LoadState.Finished)
                    {
                        Common.Log("[Launch] 自动安装整合包成功，清理安装包：" + packInstallPath);
                        if (File.Exists(packInstallPath))
                            File.Delete(packInstallPath);
                    }
                }
                catch (Common.CancelledException ex)
                {
                    Common.Log(ex, "自动安装整合包被用户取消：" + packInstallPath);
                }
                catch (Exception ex)
                {
                    Common.Log(
                        ex,
                        Lang.Text("Select.Folder.Error.InstallPack", packInstallPath),
                        Common.LogLevel.Msgbox,
                        userSummary: Lang.Text("Select.Folder.Error.InstallPack", packInstallPath));
                }

            // 确认 Minecraft 版本实例
            var selection = States.Game.SelectedInstance;
            var instance = selection == "" ? null : new McInstance(selection);
            if (instance is null || !instance.PathInstance.StartsWithF(GameFolders.mcFolderSelected) ||
                !instance.Check())
            {
                // 无效的实例
                Common.Log("[Launch] 当前选择的 Minecraft 实例无效：" + (instance is null ? "null" : instance.PathInstance),
                    instance is null ? Common.LogLevel.Normal : Common.LogLevel.Debug);
                if (InstanceList.mcInstanceListLoader.State != Common.LoadState.Finished)
                    Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                        Loading.LoaderFolderRunType.ForceRun, 1, @"versions\", true);
                if (InstanceList.mcInstanceList.Count == 0 ||
                    InstanceList.mcInstanceList.First().Value[0].Logo.Contains("RedstoneBlock"))
                {
                    instance = null;
                    States.Game.SelectedInstance = "";
                    Common.Log("[Launch] 无可用 Minecraft 实例");
                }
                else
                {
                    instance = InstanceList.mcInstanceList.First().Value[0];
                    States.Game.SelectedInstance = instance.Name;
                    Common.Log("[Launch] 自动选择 Minecraft 实例：" + instance.PathInstance);
                }
            }

            Common.RunInUi(() =>
            {
                InstanceList.McMcInstanceSelected = instance; // 绕这一圈是为了避免 McInstanceCheck 触发第二次实例改变
                isLoadFinished = true;
                RefreshButtonsUI();
                RefreshPage(false); // 有可能选择的版本变化了，需要重新刷新
                // If IsProfileVaild() = "" Then McLoginLoader.Start() '自动登录
            });
        }, "Instance Check", ThreadPriority.AboveNormal);

        // 改变页面
        RefreshPage(false);

        Animations.AniEndControl();
    }

    // 启动按钮
    public void LaunchButtonClick()
    {
        if (GameLaunch.mcLaunchLoader.State == Common.LoadState.Loading || !BtnLaunch.IsEnabled ||
            (Launcher.frmMain.pageRight is not null &&
             Launcher.frmMain.pageRight.PageState != MyPageRight.PageStates.ContentStay &&
             Launcher.frmMain.pageRight.PageState != MyPageRight.PageStates.ContentEnter))
            return;
        // 愚人节处理
        if (Launcher.isAprilEnabled && !Launcher.isAprilGiveup)
        {
            Launcher.isAprilGiveup = true;
            Launcher.frmLaunchLeft.AprilScaleTrans.ScaleX = 1d;
            Launcher.frmLaunchLeft.AprilScaleTrans.ScaleY = 1d;
            Launcher.frmLaunchLeft.AprilPosTrans.X = 0d;
            Launcher.frmLaunchLeft.AprilPosTrans.Y = 0d;
            Launcher.frmMain.BtnExtraApril.ShowRefresh();
        }

        // 实际的启动
        switch (_launchButtonAction)
        {
            case LaunchButtonAction.Launch:
            {
                if (File.Exists(InstanceList.McMcInstanceSelected.PathInstance + ".pclignore"))
                {
                    HintService.Hint(Lang.Text("Launch.Home.Instance.InstallingCannotLaunch"), HintType.Error);
                    return;
                }

                GameLaunch.McLaunchStart();
                break;
            }
            case LaunchButtonAction.Download:
            {
                Launcher.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);
                break;
            }
        }
    }

    public void RefreshButtonsUI()
    {
        if (!BtnLaunch.IsLoaded)
            return;
        // 获取当前状态
        int currentState;
        if (!isLoadFinished || InstanceList.mcInstanceListLoader.State == Common.LoadState.Loading ||
            GameFolders.McFolderListLoader.State == Common.LoadState.Loading)
        {
            currentState = 0;
        }
        else if (InstanceList.McMcInstanceSelected is null)
        {
            if (Config.Preference.Hide.PageDownload && !PageSetupUI.HiddenForceShow)
                currentState = 1;
            else
                currentState = 2;
        }
        else
        {
            currentState = 3;
        }

        // 更新状态。
        var currentLanguage = LocalizationService.CurrentLanguage.Code;
        if (currentState == btnLaunchState &&
            currentLanguage == _btnLaunchLanguage &&
            ((InstanceList.McMcInstanceSelected is null ? "" : InstanceList.McMcInstanceSelected.PathInstance) ?? "") ==
            ((btnLaunchVersion is null ? "" : btnLaunchVersion.PathInstance) ?? ""))
            goto ExitRefresh;
        _btnLaunchLanguage = currentLanguage;
        btnLaunchVersion = InstanceList.McMcInstanceSelected;
        btnLaunchState = currentState;
        switch (currentState)
        {
            case 0:
            {
                _launchButtonAction = LaunchButtonAction.Loading;
                Common.Log("[Minecraft] 启动按钮：正在加载 Minecraft 实例");
                _vm.LaunchButtonText = Lang.Text("Launch.Home.Button.Loading");
                _vm.IsLaunchButtonEnabled = false;
                _vm.VersionNameText = Lang.Text("Launch.Home.Instance.Loading");
                _vm.IsInstanceButtonEnabled = false;
                _vm.MoreButtonVisibility = Visibility.Collapsed;
                break;
            }
            case 1:
            {
                _launchButtonAction = LaunchButtonAction.Disabled;
                Common.Log("[Minecraft] 启动按钮：无 Minecraft 实例，下载已禁用");
                _vm.LaunchButtonText = Lang.Text("Launch.Home.Button.Launch");
                _vm.IsLaunchButtonEnabled = false;
                _vm.VersionNameText = Lang.Text("Launch.Home.Instance.NotFound");
                _vm.IsInstanceButtonEnabled = true;
                _vm.MoreButtonVisibility = Visibility.Collapsed;
                break;
            }
            case 2:
            {
                _launchButtonAction = LaunchButtonAction.Download;
                Common.Log("[Minecraft] 启动按钮：无 Minecraft 实例，要求下载");
                _vm.LaunchButtonText = Lang.Text("Launch.Home.Button.Download");
                _vm.IsLaunchButtonEnabled = true;
                _vm.VersionNameText = Lang.Text("Launch.Home.Instance.NotFound");
                _vm.IsInstanceButtonEnabled = true;
                _vm.MoreButtonVisibility = Visibility.Collapsed;
                break;
            }
            case 3:
            {
                _launchButtonAction = LaunchButtonAction.Launch;
                Common.Log("[Minecraft] 启动按钮：Minecraft 实例：" + InstanceList.McMcInstanceSelected.PathInstance);
                _vm.LaunchButtonText = Lang.Text("Launch.Home.Button.Launch");
                _vm.IsInstanceButtonEnabled = true;
                if (ProfileService.Current is not null)
                    _vm.IsLaunchButtonEnabled = true;
                else
                    _vm.IsLaunchButtonEnabled = false;
                _vm.VersionNameText = InstanceList.McMcInstanceSelected.Name;
                break;
            }
            // FrmLaunchLeft.BtnMore.Visibility = Visibility.Visible '由功能隐藏设置修改
        }

        ExitRefresh: ;

        // 功能隐藏
        _vm.InstanceButtonVisibility =
            !PageSetupUI.HiddenForceShow && Config.Preference.Hide.FunctionSelect
                ? Visibility.Collapsed
                : Visibility.Visible;
        if (currentState == 3) _vm.MoreButtonVisibility = _vm.InstanceButtonVisibility;
    }

    /// <summary>
    ///     每 0.2s 执行一次，刷新启动的数据 UI 显示。
    /// </summary>
    public void LaunchingRefresh()
    {
        try
        {
            if (GameLaunch.mcLaunchLoaderReal.State == Common.LoadState.Aborted)
                return;
            // 阶段状态获取
            var isLaunched = false; // 是否已经启动游戏，只是在等待窗口
            do
            {
                try
                {
                    var exitTry = false;
                    foreach (var Loader in GameLaunch.mcLaunchLoaderReal.GetLoaderList(false))
                        if (Loader.State == Common.LoadState.Loading || Loader.State == Common.LoadState.Waiting)
                        {
                            _vm.LaunchingStageText = Loader.name;
                            isLaunched = Loader.name == StageWaitWindow || Loader.name == StageEnd;
                            exitTry = true;
                            break;
                        }

                    if (exitTry) break;
                    _vm.LaunchingStageText = Lang.Text("Launch.Status.Completed");
                }
                catch (Exception ex)
                {
                    Common.Log(ex, "获取是否启动完成失败，可能是由于启动状态改变导致集合已修改");
                    return;
                }
            } while (false);

            if (Animations.AniIsRun("Launch State Page"))
                isLaunched = false; // 等待页面切换动画完成
            // 计算应显示的进度
            var actualProgress = GameLaunch.mcLaunchLoaderReal.Progress;
            if (actualProgress >= showProgress)
                showProgress += (actualProgress - showProgress) * 0.2d + 0.005d; // 向实际进度靠一点
            if (actualProgress <= showProgress)
                showProgress = actualProgress; // 原来或处理后变得比实际进度高，直接回退
            if (isLaunched)
                showProgress = 1d; // 如果已经完成了，就不卖关子了
            // 文本
            _vm.LaunchingTitleText = isLaunched ? Lang.Text("Launch.Status.Title.Launched") :
                GameLaunch.currentLaunchOptions.SaveBatch is null ? Lang.Text("Launch.Status.Title.Launching") : Lang.Text("Launch.Status.Title.ExportingScript");
            _vm.ApplyLaunchingProgress(showProgress);
            var hasLaunchDownloader = false;
            try
            {
                foreach (var Loader in NetFacade.NetManager.Tasks)
                    if (Loader.RealParent is not null && Loader.RealParent.name == StageRoot &&
                        Loader.State == Common.LoadState.Loading)
                        hasLaunchDownloader = true;
            }
            catch (Exception ex)
            {
                Common.Log(ex, "获取 Minecraft 启动下载器失败，可能是因为启动被取消");
                hasLaunchDownloader = false;
            }

            _vm.ApplyLaunchingDownload(NetFacade.NetManager.Speed);
            var shouldShowHint = Config.Preference.ShowLaunchingHint;
            // 进度改变动画
            var animList = new List<Animations.AniData>
            {
                Animations.AaGridLengthWidth(ProgressLaunchingFinished,
                    showProgress - ProgressLaunchingFinished.Width.Value, 260,
                    ease: new Animations.AniEaseOutFluent()),
                Animations.AaGridLengthWidth(ProgressLaunchingUnfinished,
                    1d - showProgress - ProgressLaunchingUnfinished.Width.Value, 260,
                    ease: new Animations.AniEaseOutFluent())
            };
            var isDownloadStateChanged =
                hasLaunchDownloader == (_vm.LaunchingDownloadVisibility == Visibility.Collapsed);
            if (isDownloadStateChanged)
            {
                _vm.LaunchingDownloadVisibility = Visibility.Visible;
                _vm.LaunchingDownloadVisibility = Visibility.Visible;
                animList.AddRange(new[]
                {
                    Animations.AaOpacity(LabLaunchingDownload,
                        (hasLaunchDownloader ? 1 : 0) - LabLaunchingDownload.Opacity, 100),
                    Animations.AaOpacity(LabLaunchingDownloadLeft,
                        (hasLaunchDownloader ? 0.5d : 0d) - LabLaunchingDownloadLeft.Opacity, 100),
                    Animations.AaCode(() =>
                    {
                        if (!hasLaunchDownloader)
                        {
                            _vm.LaunchingDownloadVisibility = Visibility.Collapsed;
                            _vm.LaunchingDownloadVisibility = Visibility.Collapsed;
                        }
                    }, 110)
                });
            }

            var isProgressStateChanged = !isLaunched == (_vm.LaunchingProgressVisibility == Visibility.Collapsed);
            if (isProgressStateChanged)
            {
                _vm.LaunchingProgressVisibility = Visibility.Visible;
                _vm.LaunchingProgressVisibility = Visibility.Visible;
                if (isLaunched && shouldShowHint) _vm.LaunchingHintVisibility = Visibility.Visible;
                animList.AddRange(new[]
                {
                    Animations.AaOpacity(LabLaunchingProgress, (!isLaunched ? 1 : 0) - LabLaunchingProgress.Opacity,
                        100),
                    Animations.AaOpacity(LabLaunchingProgressLeft,
                        (!isLaunched ? 0.5d : 0d) - LabLaunchingProgressLeft.Opacity, 100),
                    Animations.AaOpacity(PanLaunchingHint,
                        (isLaunched && shouldShowHint ? 1 : 0) - PanLaunchingHint.Opacity, 100)
                });
            }

            Animations.AniStart(animList, "Launching Progress");
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Minecraft.Launch.Error.RefreshInfo"),
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Launch.Error.RefreshInfo"));
        }
    }

    private void PanLaunchingInfo_SizeChangedW(object sender, SizeChangedEventArgs e)
    {
        var deltaWidth = e.NewSize.Width - e.PreviousSize.Width;
        if (e.PreviousSize.Width == 0d || isWidthAnimating || Math.Abs(deltaWidth) < 1d ||
            PanLaunchingInfo.ActualWidth == 0d)
            return;
        Animations.AniStart(new[]
        {
            Animations.AaWidth(PanLaunchingInfo, deltaWidth, 180, ease: new Animations.AniEaseOutFluent()),
            Animations.AaCode(() =>
            {
                isWidthAnimating = false;
                PanLaunchingInfo.Width = actualUsedWidth;
            }, after: true)
        }, "Launching Info Width");
        isWidthAnimating = true;
        actualUsedWidth = PanLaunchingInfo.Width;
        PanLaunchingInfo.Width = e.PreviousSize.Width;
    }

    private void PanLaunchingInfo_SizeChangedH(object sender, SizeChangedEventArgs e)
    {
        var deltaHeight = e.NewSize.Height - e.PreviousSize.Height;
        if (e.PreviousSize.Height == 0d || isHeightAnimating || Math.Abs(deltaHeight) < 1d ||
            PanLaunchingInfo.ActualHeight == 0d)
            return;
        Animations.AniStart(new[]
        {
            Animations.AaHeight(PanLaunchingInfo, deltaHeight, 180, ease: new Animations.AniEaseOutFluent()),
            Animations.AaCode(() =>
            {
                isHeightAnimating = false;
                PanLaunchingInfo.Height = actualUsedHeight;
            }, after: true)
        }, "Launching Info Height");
        isHeightAnimating = true;
        actualUsedHeight = PanLaunchingInfo.Height;
        PanLaunchingInfo.Height = e.PreviousSize.Height;
    }

    // 启动游戏按钮
    private void BtnLaunch_Click(object sender, MouseButtonEventArgs e)
    {
        LaunchButtonClick();
    }

    #region 切换大页面

    /// <summary>
    ///     切换至启动中页面。
    /// </summary>
    public void PageChangeToLaunching()
    {
        // 修改验证方式
        switch (ProfileService.Current?.ProfileType)
        {
            case ProfileType.Offline:
            {
                _vm.LaunchingMethodText = Lang.Text("Launch.Account.Type.Offline");
                break;
            }
            case ProfileType.Microsoft:
            {
                _vm.LaunchingMethodText = Lang.Text("Launch.Account.Type.Microsoft");
                break;
            }
            case ProfileType.Authlib:
            case ProfileType.YggdrasilConnect:
            {
                _vm.LaunchingMethodText = Lang.Text("Launch.Account.Type.ThirdParty") + (!string.IsNullOrEmpty(ProfileService.Current?.ServerName)
                    ? " / " + ProfileService.Current.ServerName
                    : "");
                break;
            }
        }

        // 初始化页面
        _vm.LaunchingNameText = InstanceList.McMcInstanceSelected.Name;
        _vm.LaunchingStageText = Lang.Text("Common.Action.Initialize");
        _vm.LaunchingTitleText = GameLaunch.currentLaunchOptions?.SaveBatch is null
            ? Lang.Text("Launch.Status.Title.Launching")
            : Lang.Text("Launch.Status.Title.ExportingScript");
        _vm.LaunchingProgressText = Lang.Number(0d, "P2");
        LabLaunchingProgress.Opacity = 1d;
        _vm.LaunchingDownloadVisibility = Visibility.Visible;
        LabLaunchingProgressLeft.Opacity = 0.6d;
        _vm.LaunchingDownloadVisibility = Visibility.Visible;
        _vm.ApplyLaunchingDownload(0);
        LabLaunchingDownload.Opacity = 0d;
        _vm.LaunchingDownloadVisibility = Visibility.Collapsed;
        LabLaunchingDownloadLeft.Opacity = 0d;
        _vm.LaunchingDownloadVisibility = Visibility.Collapsed;
        ProgressLaunchingFinished.Width = new GridLength(0d, GridUnitType.Star);
        ProgressLaunchingUnfinished.Width = new GridLength(1d, GridUnitType.Star);
        PanLaunchingHint.Opacity = 0d;
        _vm.LaunchingHintVisibility = Visibility.Collapsed;
        PanLaunchingInfo.Width = double.NaN; // 重置宽度改变动画
        GameLaunch.mcLaunchProcess = null;
        GameLaunch.mcLaunchWatcher = null;

        _vm.ApplyLaunchingHint(Config.Preference.ShowLaunchingHint);

        // 初始化其他页面
        PanInput.IsHitTestVisible = false;
        PanLaunching.IsHitTestVisible = false;
        LoadLaunching.State.LoadingState = MyLoading.MyLoadingState.Run;
        PanLaunching.Visibility = Visibility.Visible;
        Animations.AniStart(
            new[]
            {
                Animations.AaOpacity(PanInput, 0d, 50),
                Animations.AaOpacity(PanInput, -PanInput.Opacity, 110, ease: new Animations.AniEaseInFluent(),
                    after: true),
                Animations.AaScaleTransform(PanInput, 1.2d - ((ScaleTransform)PanInput.RenderTransform).ScaleX, 160),
                Animations.AaOpacity(PanLaunching, 1d - PanLaunching.Opacity, 150, 100),
                Animations.AaScaleTransform(PanLaunching, 1d - ((ScaleTransform)PanLaunching.RenderTransform).ScaleX,
                    500, 100, new Animations.AniEaseOutBack(Animations.AniEasePower.Weak)),
                Animations.AaCode(() => PanLaunching.IsHitTestVisible = true, 150)
            }, "Launch State Page"); // 略作延迟，这样如果预检测失败，不会出现奇怪的弹一下的动画
    }

    /// <summary>
    ///     切换至登录页面。
    /// </summary>
    public void PageChangeToLogin()
    {
        if (PageGet(pageCurrent) is ILoginPage loginPage) loginPage.Reload();
        PanInput.IsHitTestVisible = false;
        PanLaunching.IsHitTestVisible = false;
        LoadLaunching.State.LoadingState = MyLoading.MyLoadingState.Stop;
        PanInput.Visibility = Visibility.Visible;
        Animations.AniStart(
            new[]
            {
                Animations.AaOpacity(PanLaunching, -PanLaunching.Opacity, 150),
                Animations.AaScaleTransform(PanLaunching,
                    0.8d - ((ScaleTransform)PanLaunching.RenderTransform).ScaleX, 150,
                    ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak)),
                Animations.AaOpacity(PanInput, 1d - PanInput.Opacity, 250, 50),
                Animations.AaScaleTransform(PanInput, 1d - ((ScaleTransform)PanInput.RenderTransform).ScaleX, 300, 50,
                    new Animations.AniEaseOutBack(Animations.AniEasePower.Weak)),
                Animations.AaCode(() => PanInput.IsHitTestVisible = true, 200)
            }, "Launch State Page", true);
    }

    #endregion

    #region 切换登录页面

    private enum PageType
    {
        None,
        Auth,
        Ms,
        Profile,
        ProfileSkin,
        Offline
    }

    /// <summary>
    ///     当前页面的种类。
    /// </summary>
    private PageType pageCurrent = PageType.None;

    private object PageGet(PageType type)
    {
        switch (type)
        {
            case PageType.Auth:
            {
                if (Launcher.frmLoginAuth is null)
                    Launcher.frmLoginAuth = new PageLoginAuth();
                return Launcher.frmLoginAuth;
            }
            case PageType.Ms:
            {
                if (Launcher.frmLoginMs is null)
                    Launcher.frmLoginMs = new PageLoginMs();
                return Launcher.frmLoginMs;
            }
            case PageType.Profile:
            {
                if (Launcher.frmLoginProfile is null)
                    Launcher.frmLoginProfile = new PageLoginProfile();
                return Launcher.frmLoginProfile;
            }
            case PageType.ProfileSkin:
            {
                if (Launcher.frmLoginProfileSkin is null)
                    Launcher.frmLoginProfileSkin = new PageLoginProfileSkin();
                return Launcher.frmLoginProfileSkin;
            }
            case PageType.Offline:
            {
                if (Launcher.frmLoginOffline is null)
                    Launcher.frmLoginOffline = new PageLoginOffline();
                return Launcher.frmLoginOffline;
            }

            default:
            {
                throw new ArgumentOutOfRangeException("Type", "即将切换的登录分页编号越界");
            }
        }
    }

    /// <summary>
    ///     切换现有登录页面种类，返回新页面的实例。
    /// </summary>
    /// <param name="type">新页面的种类。</param>
    /// <param name="anim">是否显示动画。</param>
    private object PageChange(PageType type, bool anim)
    {
        object pageNew = Launcher.frmLoginMs; // 初始化一个东西，避免在执行时出现异常导致雪崩
        try
        {
            #region 确定更改的页面实例并实例化

            if (pageCurrent == type)
                return pageNew;
            pageNew = PageGet(type);

            #endregion

            #region 切换页面

            Animations.AniStop("FrmLogin PageChange");
            // 清除页面关联性
            if (pageNew is FrameworkElement element && element.Parent is not null)
            {
                element.SetValue(ContentPresenter.ContentProperty, null);
            }
            if (anim)
            {
                // 动画
                // 执行动画
                Dispatcher.Invoke(() => Animations.AniStart(new[]
                {
                    Animations.AaOpacity(PanLogin, -PanLogin.Opacity, 100, ease: new Animations.AniEaseOutFluent()),
                    Animations.AaCode(() =>
                    {
                        Animations.AniBeginControl();
                        PanLogin.Children.Clear();
                        PanLogin.Children.Add((UIElement)pageNew);
                        Animations.AniEndControl();
                    }, 100),
                    Animations.AaOpacity(PanLogin, 1d, 100, 120, new Animations.AniEaseInFluent())
                }, "FrmLogin PageChange"), DispatcherPriority.Render);
            }
            else
            {
                // 无动画
                Animations.AniBeginControl();
                PanLogin.Children.Clear();
                PanLogin.Children.Add((UIElement)pageNew);
                Animations.AniEndControl();
            }

            #endregion

            pageCurrent = type;
            return pageNew;
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Launch.Account.Error.SwitchPage", Common.GetStringFromEnum(type)),
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Launch.Account.Error.SwitchPage", Common.GetStringFromEnum(type)));
            return pageNew;
        }
    }

    /// <summary>
    ///     确认当前显示的子页面正确，并刷新该页面。
    /// </summary>
    /// <param name="anim">是否显示动画</param>
    /// <param name="targetLoginType">目标验证方式，若正在创建档案需填</param>
    public void RefreshPage(bool anim, GameLaunch.McLoginType targetLoginType = default)
    {
        var type = default(PageType);
        if (targetLoginType != default)
        {
            if (targetLoginType == GameLaunch.McLoginType.Ms)
                type = PageType.Ms;
            if (targetLoginType == GameLaunch.McLoginType.Auth)
                type = PageType.Auth;
            if (targetLoginType == GameLaunch.McLoginType.Legacy)
                type = PageType.Offline;
        }
        else if (ProfileService.Current is not null)
        {
            type = PageType.ProfileSkin;
            _vm.IsLaunchButtonEnabled = true;
        }
        else
        {
            type = PageType.Profile;
            if (_launchButtonAction != LaunchButtonAction.Download)
                _vm.IsLaunchButtonEnabled = false;
        }

        // 刷新页面
        if (pageCurrent == type)
            return;
        PageChange(type, anim);
    }

    #endregion

    #region 皮肤

    // 正版皮肤
    public static Loading.LoaderTask<Common.EqualableList<string>, string> skinMs = new("Loader Skin Ms", SkinMsLoad,
        inputDelegate: SkinMsInput, priority: ThreadPriority.AboveNormal);

    private static Common.EqualableList<string> SkinMsInput()
    {
        // 获取名称
        return new Common.EqualableList<string>
            { ProfileService.Current?.UserName ?? "", ProfileService.Current?.Uuid ?? "" };
    }

    private static void SkinMsLoad(Loading.LoaderTask<Common.EqualableList<string>, string> data)
    {
        // 清空已有皮肤
        // 如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
        Common.RunInUi(() =>
        {
            if (Launcher.frmLoginProfileSkin is not null && Launcher.frmLoginProfileSkin.Skin is not null)
                Launcher.frmLoginProfileSkin.Skin.Clear();
        });
        // 获取 Url
        var userName = data.input[0];
        var uuid = data.input[1];
        if (ProfileService.Current is not null)
        {
            userName = ProfileService.Current.UserName;
            uuid = ProfileService.Current.Uuid;
        }

        if (string.IsNullOrEmpty(userName))
        {
            data.output = Common.PathImage + "Skins/" + Skin.McSkinVariant(ProfileUi.GetOfflineUuid(userName)) +
                          ".png";
            Common.Log("[Minecraft] 获取微软正版皮肤失败，ID 为空");
            goto Finish;
        }

        try
        {
            var result = Skin.McSkinGetAddress(uuid, "Ms");
            if (data.IsAborted)
                throw new ThreadInterruptedException("当前任务已取消：" + userName);
            result = Skin.McSkinDownload(result);
            if (data.IsAborted)
                throw new ThreadInterruptedException("当前任务已取消：" + userName);
            data.output = result;
        }
        catch (Exception ex)
        {
            if (ex is ThreadInterruptedException)
            {
                data.output = "";
                Common.Log("[Minecraft] 已取消皮肤获取：" + userName);
                return;
            }

            if (ex.ToString().Contains("429"))
            {
                data.output = Common.PathImage + "Skins/" +
                              Skin.McSkinVariant(ProfileUi.GetOfflineUuid(userName)) + ".png";
                Common.Log(
                    Lang.Text("Launch.Skin.Error.MsRateLimited", userName),
                    Common.LogLevel.Hint,
                    userSummary: Lang.Text("Launch.Skin.Error.MsRateLimited", userName));
            }
            else if (ex.ToString().Contains("未设置自定义皮肤"))
            {
                data.output = Common.PathImage + "Skins/" +
                               Skin.McSkinVariant(ProfileUi.GetOfflineUuid(userName)) + ".png";
                Common.Log("[Minecraft] 用户未设置自定义皮肤，跳过皮肤加载");
            }
            else
            {
                data.output = Common.PathImage + "Skins/" +
                               Skin.McSkinVariant(ProfileUi.GetOfflineUuid(userName)) + ".png";
                Common.Log(
                    ex,
                    Lang.Text("Launch.Skin.Error.MsGet", userName),
                    Common.LogLevel.Hint,
                    userSummary: Lang.Text("Launch.Skin.Error.MsGet", userName));
            }
        }

        Finish: ;

        // 刷新显示
        if (Launcher.frmLoginProfileSkin is not null && ReferenceEquals(Launcher.frmLoginProfileSkin.Skin.loader, data))
            Common.RunInUi(Launcher.frmLoginProfileSkin.Skin.Load);
        else if (!data.IsAborted) // 如果已经中断，Input 也被清空，就不会再次刷新
            data.input = null; // 清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
    }

    // 离线皮肤
    public static Loading.LoaderTask<Common.EqualableList<string>, string> skinLegacy = new("Loader Skin Legacy",
        SkinLegacyLoad, inputDelegate: SkinLegacyInput, priority: ThreadPriority.AboveNormal);

    private static Common.EqualableList<string> SkinLegacyInput()
    {
        return new Common.EqualableList<string>
            { ProfileService.Current?.UserName ?? "", ProfileService.Current?.Uuid ?? "" };
    }

    private static void SkinLegacyLoad(Loading.LoaderTask<Common.EqualableList<string>, string> data)
    {
        // 清空已有皮肤
        Common.RunInUi(() =>
        {
            if (Launcher.frmLoginProfileSkin is not null && Launcher.frmLoginProfileSkin.Skin is not null)
                Launcher.frmLoginProfileSkin.Skin.Clear();
        });
        data.output = Common.PathImage + "Skins/" + Skin.McSkinVariant(data.input[1]) + ".png";
        // 刷新显示
        if (Launcher.frmLoginProfileSkin is not null && ReferenceEquals(Launcher.frmLoginProfileSkin.Skin.loader, data))
            Common.RunInUi(() => Launcher.frmLoginProfileSkin.Skin.Load());
        else if (!data.IsAborted) // 如果已经中断，Input 也被清空，就不会再次刷新
            data.input = null; // 清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
    }

    // Authlib-Injector 皮肤
    public static Loading.LoaderTask<Common.EqualableList<string>, string> skinAuth = new("Loader Skin Auth",
        SkinAuthLoad, inputDelegate: SkinAuthInput, priority: ThreadPriority.AboveNormal);

    private static Common.EqualableList<string> SkinAuthInput()
    {
        // 获取名称
        return new Common.EqualableList<string>
            { ProfileService.Current?.UserName ?? "", ProfileService.Current?.Uuid ?? "" };
    }

    private static void SkinAuthLoad(Loading.LoaderTask<Common.EqualableList<string>, string> data)
    {
        // 清空已有皮肤
        // 如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
        Common.RunInUi(() =>
        {
            if (Launcher.frmLoginProfileSkin is not null && Launcher.frmLoginProfileSkin.Skin is not null)
                Launcher.frmLoginProfileSkin.Skin.Clear();
        });
        // 获取 Url
        var userName = data.input[0];
        var uuid = data.input[1];
        if (string.IsNullOrEmpty(userName))
        {
            data.output = Common.PathImage + "Skins/Steve.png";
            Common.Log("[Minecraft] 获取 Authlib-Injector 皮肤失败，ID 为空");
            goto Finish;
        }

        try
        {
            var result = Skin.McSkinGetAddress(uuid, "Auth");
            if (data.IsAborted)
                throw new ThreadInterruptedException("当前任务已取消：" + userName);
            result = Skin.McSkinDownload(result);
            if (data.IsAborted)
                throw new ThreadInterruptedException("当前任务已取消：" + userName);
            data.output = result;
        }
        catch (Exception ex)
        {
            if (ex is ThreadInterruptedException)
            {
                data.output = "";
                return;
            }

            if (ex.ToString().Contains("429"))
            {
                data.output = Common.PathImage + "Skins/Steve.png";
                Common.Log(
                    $"[Minecraft] 获取 Authlib-Injector 皮肤失败（{userName}）：获取皮肤太过频繁，请 5 分钟后再试！",
                    Common.LogLevel.Hint,
                    userSummary: Lang.Text("Launch.Skin.Error.AuthlibRateLimited"));
            }
            else if (ex.ToString().Contains("未设置自定义皮肤"))
            {
                data.output = Common.PathImage + "Skins/Steve.png";
                Common.Log("[Minecraft] 用户未设置自定义皮肤，跳过皮肤加载");
            }
            else
            {
                data.output = Common.PathImage + "Skins/Steve.png";
                Common.Log(
                    ex,
                    Lang.Text("Launch.Skin.Error.AuthGet", userName),
                    Common.LogLevel.Hint,
                    userSummary: Lang.Text("Launch.Skin.Error.AuthGet", userName));
            }
        }

        Finish: ;

        // 刷新显示
        if (Launcher.frmLoginProfileSkin is not null && ReferenceEquals(Launcher.frmLoginProfileSkin.Skin.loader, data))
            Common.RunInUi(Launcher.frmLoginProfileSkin.Skin.Load);
        else if (!data.IsAborted) // 如果已经中断，Input 也被清空，就不会再次刷新
            data.input = null; // 清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
    }

    // 全部皮肤加载器
    // 需要放在其中元素的后面，否则会因为它提前被加载而莫名其妙变成 Nothing
    public static List<Loading.LoaderTask<Common.EqualableList<string>, string>> skinLoaders = new()
        { skinMs, skinLegacy, skinAuth };

    #endregion
}
