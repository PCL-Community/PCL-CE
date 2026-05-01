using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

public partial class PageLaunchLeft
{
    private double ActualUsedHeight;
    private double ActualUsedWidth;
    private int BtnLaunchState;
    private ModMinecraft.McInstance BtnLaunchVersion;
    private bool IsHeightAnimating;
    public interface ILoginPage { void Reload(); }

    // 加载当前实例
    private bool IsLoad;

    private bool IsLoadFinished;

    // 尺寸改变动画
    private bool IsWidthAnimating;
    private double ShowProgress;

    public PageLaunchLeft()
    {
        InitializeComponent();
        Loaded += PageLaunchLeft_Loaded;
        // Handles
        BtnInstance.Click += BtnInstance_Click;
        BtnLaunch.Click += BtnLaunch_Click;
        BtnLaunch.Loaded += (_, _) => RefreshButtonsUI();
        BtnCancel.Click += BtnCancel_Click;
        BtnMore.Click += BtnMore_Click;
        PanLaunchingInfo.SizeChanged += PanLaunchingInfo_SizeChangedW;
        PanLaunchingInfo.SizeChanged += PanLaunchingInfo_SizeChangedH;
    }

    public void PageLaunchLeft_Loaded(object sender, RoutedEventArgs e)
    {
        if (IsLoad)
            RefreshPage(false);

        AprilPosTrans.X = 0d;
        AprilPosTrans.Y = 0d;

        if (IsLoad)
            return;
        IsLoad = true;
        ModAnimation.AniControlEnabled += 1;

        // 开始按钮
        ModMinecraft.McInstanceListLoader.LoadingStateChanged += (_, _) => RefreshButtonsUI();
        ModMinecraft.McFolderListLoader.LoadingStateChanged += (_, _) => RefreshButtonsUI();
        RefreshButtonsUI();

        // 初始化档案
        ModProfile.GetProfile();
        if (!(ModProfile.ProfileList.Count == 0) && ModProfile.LastUsedProfile >= 0 &&
            ModProfile.LastUsedProfile < ModProfile.ProfileList.Count)
            ModProfile.SelectedProfile = ModProfile.ProfileList[ModProfile.LastUsedProfile];

        // 加载实例
        ModBase.RunInNewThread(() =>
        {
            // 自动整合包安装：准备
            string PackInstallPath = null;
            if (File.Exists(ModBase.ExePath + "modpack.zip"))
                PackInstallPath = ModBase.ExePath + "modpack.zip";
            if (File.Exists(ModBase.ExePath + "modpack.mrpack"))
                PackInstallPath = ModBase.ExePath + "modpack.mrpack";
            if (PackInstallPath is not null)
            {
                ModBase.Log("[Launch] 需自动安装整合包：" + PackInstallPath, ModBase.LogLevel.Debug);
                States.Game.SelectedFolder = @"$.minecraft\";
                if (!Directory.Exists(ModBase.ExePath + @".minecraft\"))
                {
                    Directory.CreateDirectory(ModBase.ExePath + @".minecraft\");
                    Directory.CreateDirectory(ModBase.ExePath + @".minecraft\versions\");
                    ModMinecraft.McFolderLauncherProfilesJsonCreate(ModBase.ExePath + @".minecraft\");
                }

                PageSelectLeft.AddFolder(ModBase.ExePath + @".minecraft\",
                    ModBase.GetFolderNameFromPath(ModBase.ExePath), false);
                ModMinecraft.McFolderListLoader.WaitForExit();
            }

            // 确认 Minecraft 文件夹存在
            ModMinecraft.McFolderSelected =
                States.Game.SelectedFolder.ToString().Replace("$", ModBase.ExePath);
            if (string.IsNullOrEmpty(ModMinecraft.McFolderSelected) || !Directory.Exists(ModMinecraft.McFolderSelected))
            {
                // 无效的文件夹
                if (string.IsNullOrEmpty(ModMinecraft.McFolderSelected))
                    ModBase.Log("[Launch] 没有已储存的 Minecraft 文件夹");
                else
                    ModBase.Log("[Launch] Minecraft 文件夹无效，该文件夹已不存在：" + ModMinecraft.McFolderSelected,
                        ModBase.LogLevel.Debug);
                ModMinecraft.McFolderListLoader.WaitForExit(IsForceRestart: true);
                States.Game.SelectedFolder = ModMinecraft.McFolderList[0].Location.Replace(ModBase.ExePath, "$");
            }

            ModBase.Log("[Launch] Minecraft 文件夹：" + ModMinecraft.McFolderSelected);
            if (Config.Debug.AddRandomDelay)
                Thread.Sleep(RandomUtils.NextInt(500, 3000));
            // 自动整合包安装
            if (PackInstallPath is not null)
                try
                {
                    var InstallLoader = ModModpack.ModpackInstall(PackInstallPath);
                    ModBase.Log("[Launch] 自动安装整合包已开始：" + PackInstallPath);
                    InstallLoader.WaitForExit();
                    if (InstallLoader.State == ModBase.LoadState.Finished)
                    {
                        ModBase.Log("[Launch] 自动安装整合包成功，清理安装包：" + PackInstallPath);
                        if (File.Exists(PackInstallPath))
                            File.Delete(PackInstallPath);
                    }
                }
                catch (ModBase.CancelledException ex)
                {
                    ModBase.Log(ex, "自动安装整合包被用户取消：" + PackInstallPath);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "自动安装整合包失败：" + PackInstallPath, ModBase.LogLevel.Msgbox);
                }

            // 确认 Minecraft 版本实例
            var Selection = States.Game.SelectedInstance;
            var Instance = Selection == "" ? null : new ModMinecraft.McInstance(Selection);
            if (Instance is null || !Instance.PathInstance.StartsWithF(ModMinecraft.McFolderSelected) ||
                !Instance.Check())
            {
                // 无效的实例
                ModBase.Log("[Launch] 当前选择的 Minecraft 实例无效：" + (Instance is null ? "null" : Instance.PathInstance),
                    Instance == null ? ModBase.LogLevel.Normal : ModBase.LogLevel.Debug);
                if (ModMinecraft.McInstanceListLoader.State != ModBase.LoadState.Finished)
                    ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                        ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\", true);
                if (ModMinecraft.McInstanceList.Count == 0 ||
                    ModMinecraft.McInstanceList.First().Value[0].Logo.Contains("RedstoneBlock"))
                {
                    Instance = null;
                    States.Game.SelectedInstance = "";
                    ModBase.Log("[Launch] 无可用 Minecraft 实例");
                }
                else
                {
                    Instance = ModMinecraft.McInstanceList.First().Value[0];
                    States.Game.SelectedInstance = Instance.Name;
                    ModBase.Log("[Launch] 自动选择 Minecraft 实例：" + Instance.PathInstance);
                }
            }

            ModBase.RunInUi(() =>
            {
                ModMinecraft.McInstanceSelected = Instance; // 绕这一圈是为了避免 McInstanceCheck 触发第二次实例改变
                IsLoadFinished = true;
                RefreshButtonsUI();
                RefreshPage(false); // 有可能选择的版本变化了，需要重新刷新
                // If IsProfileVaild() = "" Then McLoginLoader.Start() '自动登录
            });
        }, "Instance Check", ThreadPriority.AboveNormal);

        // 改变页面
        RefreshPage(false);

        ModAnimation.AniControlEnabled -= 1;
    }

    // 实例选择按钮
    private void BtnInstance_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModLaunch.McLaunchLoader.State == ModBase.LoadState.Loading)
            return;
        ModMain.FrmMain.PageChange(FormMain.PageType.InstanceSelect);
    }

    // 启动按钮
    public void LaunchButtonClick()
    {
        if (ModLaunch.McLaunchLoader.State == ModBase.LoadState.Loading || !BtnLaunch.IsEnabled ||
            (ModMain.FrmMain.PageRight is not null &&
             ModMain.FrmMain.PageRight.PageState != MyPageRight.PageStates.ContentStay &&
             ModMain.FrmMain.PageRight.PageState != MyPageRight.PageStates.ContentEnter))
            return;
        // 愚人节处理
        if (ModMain.IsAprilEnabled && !ModMain.IsAprilGiveup)
        {
            ModMain.IsAprilGiveup = true;
            ModMain.FrmLaunchLeft.AprilScaleTrans.ScaleX = 1d;
            ModMain.FrmLaunchLeft.AprilScaleTrans.ScaleY = 1d;
            ModMain.FrmLaunchLeft.AprilPosTrans.X = 0d;
            ModMain.FrmLaunchLeft.AprilPosTrans.Y = 0d;
            ModMain.FrmMain.BtnExtraApril.ShowRefresh();
        }

        // 实际的启动
        if (BtnLaunch.Text == "启动游戏")
        {
            if (File.Exists(ModMinecraft.McInstanceSelected.PathInstance + ".pclignore"))
            {
                ModMain.Hint("当前实例正在安装，无法启动！", ModMain.HintType.Critical);
                return;
            }

            ModLaunch.McLaunchStart();
        }
        else if (BtnLaunch.Text == "下载游戏")
        {
            ModMain.FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);
        }
    }

    public void RefreshButtonsUI()
    {
        if (!BtnLaunch.IsLoaded)
            return;
        // 获取当前状态
        int CurrentState;
        if (!IsLoadFinished || ModMinecraft.McInstanceListLoader.State == ModBase.LoadState.Loading ||
            ModMinecraft.McFolderListLoader.State == ModBase.LoadState.Loading)
        {
            CurrentState = 0;
        }
        else if (ModMinecraft.McInstanceSelected is null)
        {
            if (Config.Preference.Hide.PageDownload && !PageSetupUI.HiddenForceShow)
                CurrentState = 1;
            else
                CurrentState = 2;
        }
        else
        {
            CurrentState = 3;
        }

        // 更新状态
        if (CurrentState == BtnLaunchState &&
            ((ModMinecraft.McInstanceSelected is null ? "" : ModMinecraft.McInstanceSelected.PathInstance) ?? "") ==
            ((BtnLaunchVersion is null ? "" : BtnLaunchVersion.PathInstance) ?? ""))
            goto ExitRefresh;
        BtnLaunchVersion = ModMinecraft.McInstanceSelected;
        BtnLaunchState = CurrentState;
        switch (CurrentState)
        {
            case 0:
            {
                ModBase.Log("[Minecraft] 启动按钮：正在加载 Minecraft 实例");
                ModMain.FrmLaunchLeft.BtnLaunch.Text = "正在加载";
                ModMain.FrmLaunchLeft.BtnLaunch.IsEnabled = false;
                ModMain.FrmLaunchLeft.LabVersion.Text = "正在加载中，请稍候";
                ModMain.FrmLaunchLeft.BtnInstance.IsEnabled = false;
                ModMain.FrmLaunchLeft.BtnMore.Visibility = Visibility.Collapsed;
                break;
            }
            case 1:
            {
                ModBase.Log("[Minecraft] 启动按钮：无 Minecraft 实例，下载已禁用");
                ModMain.FrmLaunchLeft.BtnLaunch.Text = "启动游戏";
                ModMain.FrmLaunchLeft.BtnLaunch.IsEnabled = false;
                ModMain.FrmLaunchLeft.LabVersion.Text = "未找到可用的游戏实例";
                ModMain.FrmLaunchLeft.BtnInstance.IsEnabled = true;
                ModMain.FrmLaunchLeft.BtnMore.Visibility = Visibility.Collapsed;
                break;
            }
            case 2:
            {
                ModBase.Log("[Minecraft] 启动按钮：无 Minecraft 实例，要求下载");
                ModMain.FrmLaunchLeft.BtnLaunch.Text = "下载游戏";
                ModMain.FrmLaunchLeft.BtnLaunch.IsEnabled = true;
                ModMain.FrmLaunchLeft.LabVersion.Text = "未找到可用的游戏实例";
                ModMain.FrmLaunchLeft.BtnInstance.IsEnabled = true;
                ModMain.FrmLaunchLeft.BtnMore.Visibility = Visibility.Collapsed;
                break;
            }
            case 3:
            {
                ModBase.Log("[Minecraft] 启动按钮：Minecraft 实例：" + ModMinecraft.McInstanceSelected.PathInstance);
                ModMain.FrmLaunchLeft.BtnLaunch.Text = "启动游戏";
                ModMain.FrmLaunchLeft.BtnInstance.IsEnabled = true;
                if (ModProfile.SelectedProfile is not null)
                    BtnLaunch.IsEnabled = true;
                else
                    BtnLaunch.IsEnabled = false;
                ModMain.FrmLaunchLeft.LabVersion.Text = ModMinecraft.McInstanceSelected.Name;
                break;
            }
            // FrmLaunchLeft.BtnMore.Visibility = Visibility.Visible '由功能隐藏设置修改
        }

        ExitRefresh: ;

        // 功能隐藏
        ModMain.FrmLaunchLeft.BtnInstance.Visibility =
            !PageSetupUI.HiddenForceShow && Config.Preference.Hide.FunctionSelect
                ? Visibility.Collapsed
                : Visibility.Visible;
        if (CurrentState == 3) ModMain.FrmLaunchLeft.BtnMore.Visibility = ModMain.FrmLaunchLeft.BtnInstance.Visibility;
    }

    // 取消按钮
    private void BtnCancel_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModLaunch.McLaunchLoaderReal is not null)
        {
            ModLaunch.McLaunchLoaderReal.Abort();
            ModLaunch.McLaunchLog("已取消启动");
            try
            {
                if (ModLaunch.McLaunchWatcher is not null)
                    ModLaunch.McLaunchWatcher.Kill();
                else if (ModLaunch.McLaunchProcess is not null)
                    if (!ModLaunch.McLaunchProcess.HasExited)
                        ModLaunch.McLaunchProcess.Kill();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "取消启动结束进程失败", ModBase.LogLevel.Hint);
            }
        }
    }

    // 实例设置按钮
    private void BtnMore_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModLaunch.McLaunchLoader.State == ModBase.LoadState.Loading)
            return;
        ModMinecraft.McInstanceSelected.Load();
        PageInstanceLeft.Instance = ModMinecraft.McInstanceSelected;
        if (File.Exists(ModMinecraft.McInstanceSelected.PathInstance + ".pclignore"))
        {
            ModMain.Hint("当前实例正在安装，暂无法进行实例设置！", ModMain.HintType.Critical);
            return;
        }

        ModMain.FrmMain.PageChange(FormMain.PageType.InstanceSetup);
    }

    /// <summary>
    ///     每 0.2s 执行一次，刷新启动的数据 UI 显示。
    /// </summary>
    public void LaunchingRefresh()
    {
        try
        {
            if (ModLaunch.McLaunchLoaderReal.State == ModBase.LoadState.Aborted)
                return;
            // 阶段状态获取
            var IsLaunched = false; // 是否已经启动游戏，只是在等待窗口
            do
            {
                try
                {
                    var exitTry = false;
                    foreach (var Loader in ModLaunch.McLaunchLoaderReal.GetLoaderList(false))
                        if (Loader.State == ModBase.LoadState.Loading || Loader.State == ModBase.LoadState.Waiting)
                        {
                            LabLaunchingStage.Text = Loader.Name;
                            IsLaunched = Loader.Name == "等待游戏窗口出现" || Loader.Name == "结束处理";
                            exitTry = true;
                            break;
                        }

                    if (exitTry) break;
                    LabLaunchingStage.Text = "已完成";
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "获取是否启动完成失败，可能是由于启动状态改变导致集合已修改");
                    return;
                }
            } while (false);

            if (ModAnimation.AniIsRun("Launch State Page"))
                IsLaunched = false; // 等待页面切换动画完成
            // 计算应显示的进度
            var ActualProgress = ModLaunch.McLaunchLoaderReal.Progress;
            if (ActualProgress >= ShowProgress)
                ShowProgress += (ActualProgress - ShowProgress) * 0.2d + 0.005d; // 向实际进度靠一点
            if (ActualProgress <= ShowProgress)
                ShowProgress = ActualProgress; // 原来或处理后变得比实际进度高，直接回退
            if (IsLaunched)
                ShowProgress = 1d; // 如果已经完成了，就不卖关子了
            // 文本
            LabLaunchingTitle.Text = IsLaunched ? "已启动游戏" :
                ModLaunch.CurrentLaunchOptions.SaveBatch is null ? "正在启动游戏" : "正在导出启动脚本";
            LabLaunchingProgress.Text = ModBase.StrFillNum(ShowProgress * 100d, 2) + " %";
            var HasLaunchDownloader = false;
            try
            {
                foreach (var Loader in ModNet.NetManager.Tasks)
                    if (Loader.RealParent is not null && Loader.RealParent.Name == "Minecraft 启动" &&
                        Loader.State == ModBase.LoadState.Loading)
                        HasLaunchDownloader = true;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取 Minecraft 启动下载器失败，可能是因为启动被取消");
                HasLaunchDownloader = false;
            }

            LabLaunchingDownload.Text = ModBase.GetString(ModNet.NetManager.Speed) + "/s";
            var ShouldShowHint = Config.Preference.ShowLaunchingHint;
            // 进度改变动画
            var AnimList = new List<ModAnimation.AniData>
            {
                ModAnimation.AaGridLengthWidth(ProgressLaunchingFinished,
                    ShowProgress - ProgressLaunchingFinished.Width.Value, 260,
                    Ease: new ModAnimation.AniEaseOutFluent()),
                ModAnimation.AaGridLengthWidth(ProgressLaunchingUnfinished,
                    1d - ShowProgress - ProgressLaunchingUnfinished.Width.Value, 260,
                    Ease: new ModAnimation.AniEaseOutFluent())
            };
            var IsDownloadStateChanged =
                HasLaunchDownloader == (LabLaunchingDownload.Visibility == Visibility.Collapsed);
            if (IsDownloadStateChanged)
            {
                LabLaunchingDownload.Visibility = Visibility.Visible;
                LabLaunchingDownloadLeft.Visibility = Visibility.Visible;
                AnimList.AddRange(new[]
                {
                    ModAnimation.AaOpacity(LabLaunchingDownload,
                        (HasLaunchDownloader ? 1 : 0) - LabLaunchingDownload.Opacity, 100),
                    ModAnimation.AaOpacity(LabLaunchingDownloadLeft,
                        (HasLaunchDownloader ? 0.5d : 0d) - LabLaunchingDownloadLeft.Opacity, 100),
                    ModAnimation.AaCode(() =>
                    {
                        if (!HasLaunchDownloader)
                        {
                            LabLaunchingDownload.Visibility = Visibility.Collapsed;
                            LabLaunchingDownloadLeft.Visibility = Visibility.Collapsed;
                        }
                    }, 110)
                });
            }

            var IsProgressStateChanged = !IsLaunched == (LabLaunchingProgress.Visibility == Visibility.Collapsed);
            if (IsProgressStateChanged)
            {
                LabLaunchingProgress.Visibility = Visibility.Visible;
                LabLaunchingProgressLeft.Visibility = Visibility.Visible;
                if (IsLaunched && ShouldShowHint) PanLaunchingHint.Visibility = Visibility.Visible;
                AnimList.AddRange(new[]
                {
                    ModAnimation.AaOpacity(LabLaunchingProgress, (!IsLaunched ? 1 : 0) - LabLaunchingProgress.Opacity,
                        100),
                    ModAnimation.AaOpacity(LabLaunchingProgressLeft,
                        (!IsLaunched ? 0.5d : 0d) - LabLaunchingProgressLeft.Opacity, 100),
                    ModAnimation.AaOpacity(PanLaunchingHint,
                        (IsLaunched && ShouldShowHint ? 1 : 0) - PanLaunchingHint.Opacity, 100)
                });
            }

            ModAnimation.AniStart(AnimList, "Launching Progress");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新启动信息失败", ModBase.LogLevel.Feedback);
        }
    }

    private void PanLaunchingInfo_SizeChangedW(object sender, SizeChangedEventArgs e)
    {
        var DeltaWidth = e.NewSize.Width - e.PreviousSize.Width;
        if (e.PreviousSize.Width == 0d || IsWidthAnimating || Math.Abs(DeltaWidth) < 1d ||
            PanLaunchingInfo.ActualWidth == 0d)
            return;
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaWidth(PanLaunchingInfo, DeltaWidth, 180, Ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(() =>
            {
                IsWidthAnimating = false;
                PanLaunchingInfo.Width = ActualUsedWidth;
            }, After: true)
        }, "Launching Info Width");
        IsWidthAnimating = true;
        ActualUsedWidth = PanLaunchingInfo.Width;
        PanLaunchingInfo.Width = e.PreviousSize.Width;
    }

    private void PanLaunchingInfo_SizeChangedH(object sender, SizeChangedEventArgs e)
    {
        var DeltaHeight = e.NewSize.Height - e.PreviousSize.Height;
        if (e.PreviousSize.Height == 0d || IsHeightAnimating || Math.Abs(DeltaHeight) < 1d ||
            PanLaunchingInfo.ActualHeight == 0d)
            return;
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaHeight(PanLaunchingInfo, DeltaHeight, 180, Ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(() =>
            {
                IsHeightAnimating = false;
                PanLaunchingInfo.Height = ActualUsedHeight;
            }, After: true)
        }, "Launching Info Height");
        IsHeightAnimating = true;
        ActualUsedHeight = PanLaunchingInfo.Height;
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
        switch (ModProfile.SelectedProfile.Type)
        {
            case ModLaunch.McLoginType.Legacy:
            {
                LabLaunchingMethod.Text = "离线验证";
                break;
            }
            case ModLaunch.McLoginType.Ms:
            {
                LabLaunchingMethod.Text = "正版验证";
                break;
            }
            case ModLaunch.McLoginType.Auth:
            {
                LabLaunchingMethod.Text = "第三方验证" + (!string.IsNullOrEmpty(ModProfile.SelectedProfile.ServerName)
                    ? " / " + ModProfile.SelectedProfile.ServerName
                    : "");
                break;
            }
        }

        // 初始化页面
        LabLaunchingName.Text = ModMinecraft.McInstanceSelected.Name;
        LabLaunchingStage.Text = "初始化";
        LabLaunchingTitle.Text = ModLaunch.CurrentLaunchOptions?.SaveBatch is null ? "正在启动游戏" : "正在导出启动脚本";
        LabLaunchingProgress.Text = "0.00 %";
        LabLaunchingProgress.Opacity = 1d;
        LabLaunchingDownload.Visibility = Visibility.Visible;
        LabLaunchingProgressLeft.Opacity = 0.6d;
        LabLaunchingDownload.Visibility = Visibility.Visible;
        LabLaunchingDownload.Text = "0 B/s";
        LabLaunchingDownload.Opacity = 0d;
        LabLaunchingDownload.Visibility = Visibility.Collapsed;
        LabLaunchingDownloadLeft.Opacity = 0d;
        LabLaunchingDownloadLeft.Visibility = Visibility.Collapsed;
        ProgressLaunchingFinished.Width = new GridLength(0d, GridUnitType.Star);
        ProgressLaunchingUnfinished.Width = new GridLength(1d, GridUnitType.Star);
        PanLaunchingHint.Opacity = 0d;
        PanLaunchingHint.Visibility = Visibility.Collapsed;
        PanLaunchingInfo.Width = double.NaN; // 重置宽度改变动画
        ModLaunch.McLaunchProcess = null;
        ModLaunch.McLaunchWatcher = null;

        var ShouldShowHint = Config.Preference.ShowLaunchingHint;
        if (ShouldShowHint)
            LabLaunchingHint.Text = PageLaunchRight.GetRandomHint(true, true);
        else
            LabLaunchingHint.Text = "";

        // 初始化其他页面
        PanInput.IsHitTestVisible = false;
        PanLaunching.IsHitTestVisible = false;
        LoadLaunching.State.LoadingState = MyLoading.MyLoadingState.Run;
        PanLaunching.Visibility = Visibility.Visible;
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaOpacity(PanInput, 0d, 50),
                ModAnimation.AaOpacity(PanInput, -PanInput.Opacity, 110, Ease: new ModAnimation.AniEaseInFluent(),
                    After: true),
                ModAnimation.AaScaleTransform(PanInput, 1.2d - ((ScaleTransform)PanInput.RenderTransform).ScaleX, 160),
                ModAnimation.AaOpacity(PanLaunching, 1d - PanLaunching.Opacity, 150, 100),
                ModAnimation.AaScaleTransform(PanLaunching, 1d - ((ScaleTransform)PanLaunching.RenderTransform).ScaleX,
                    500, 100, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaCode(() => PanLaunching.IsHitTestVisible = true, 150)
            }, "Launch State Page"); // 略作延迟，这样如果预检测失败，不会出现奇怪的弹一下的动画
    }

    /// <summary>
    ///     切换至登录页面。
    /// </summary>
    public void PageChangeToLogin()
    {
        if (PageGet(PageCurrent) is ILoginPage loginPage) loginPage.Reload();
        PanInput.IsHitTestVisible = false;
        PanLaunching.IsHitTestVisible = false;
        LoadLaunching.State.LoadingState = MyLoading.MyLoadingState.Stop;
        PanInput.Visibility = Visibility.Visible;
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaOpacity(PanLaunching, -PanLaunching.Opacity, 150),
                ModAnimation.AaScaleTransform(PanLaunching,
                    0.8d - ((ScaleTransform)PanLaunching.RenderTransform).ScaleX, 150,
                    Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaOpacity(PanInput, 1d - PanInput.Opacity, 250, 50),
                ModAnimation.AaScaleTransform(PanInput, 1d - ((ScaleTransform)PanInput.RenderTransform).ScaleX, 300, 50,
                    new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaCode(() => PanInput.IsHitTestVisible = true, 200)
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
    private PageType PageCurrent = PageType.None;

    private object PageGet(PageType Type)
    {
        switch (Type)
        {
            case PageType.Auth:
            {
                if (ModMain.FrmLoginAuth == null)
                    ModMain.FrmLoginAuth = new PageLoginAuth();
                return ModMain.FrmLoginAuth;
            }
            case PageType.Ms:
            {
                if (ModMain.FrmLoginMs == null)
                    ModMain.FrmLoginMs = new PageLoginMs();
                return ModMain.FrmLoginMs;
            }
            case PageType.Profile:
            {
                if (ModMain.FrmLoginProfile == null)
                    ModMain.FrmLoginProfile = new PageLoginProfile();
                return ModMain.FrmLoginProfile;
            }
            case PageType.ProfileSkin:
            {
                if (ModMain.FrmLoginProfileSkin == null)
                    ModMain.FrmLoginProfileSkin = new PageLoginProfileSkin();
                return ModMain.FrmLoginProfileSkin;
            }
            case PageType.Offline:
            {
                if (ModMain.FrmLoginOffline == null)
                    ModMain.FrmLoginOffline = new PageLoginOffline();
                return ModMain.FrmLoginOffline;
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
    /// <param name="Type">新页面的种类。</param>
    /// <param name="Anim">是否显示动画。</param>
    private object PageChange(PageType Type, bool Anim)
    {
        object PageNew = ModMain.FrmLoginMs; // 初始化一个东西，避免在执行时出现异常导致雪崩
        try
        {
            #region 确定更改的页面实例并实例化

            if (PageCurrent == Type)
                return PageNew;
            PageNew = PageGet(Type);

            #endregion

            #region 切换页面

            ModAnimation.AniStop("FrmLogin PageChange");
            // 清除页面关联性
            if (PageNew is FrameworkElement element && element.Parent != null)
            {
                element.SetValue(ContentPresenter.ContentProperty, null);
            }
            if (Anim)
            {
                // 动画
                // 执行动画
                Dispatcher.Invoke(() => ModAnimation.AniStart(new[]
                {
                    ModAnimation.AaOpacity(PanLogin, -PanLogin.Opacity, 100, Ease: new ModAnimation.AniEaseOutFluent()),
                    ModAnimation.AaCode(() =>
                    {
                        ModAnimation.AniControlEnabled += 1;
                        PanLogin.Children.Clear();
                        PanLogin.Children.Add((UIElement)PageNew);
                        ModAnimation.AniControlEnabled -= 1;
                    }, 100),
                    ModAnimation.AaOpacity(PanLogin, 1d, 100, 120, new ModAnimation.AniEaseInFluent())
                }, "FrmLogin PageChange"), DispatcherPriority.Render);
            }
            else
            {
                // 无动画
                ModAnimation.AniControlEnabled += 1;
                PanLogin.Children.Clear();
                PanLogin.Children.Add((UIElement)PageNew);
                ModAnimation.AniControlEnabled -= 1;
            }

            #endregion

            PageCurrent = Type;
            return PageNew;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "切换登录分页失败（" + ModBase.GetStringFromEnum(Type) + "）", ModBase.LogLevel.Feedback);
            return PageNew;
        }
    }

    /// <summary>
    ///     确认当前显示的子页面正确，并刷新该页面。
    /// </summary>
    /// <param name="Anim">是否显示动画</param>
    /// <param name="TargetLoginType">目标验证方式，若正在创建档案需填</param>
    public void RefreshPage(bool Anim, ModLaunch.McLoginType TargetLoginType = default)
    {
        var Type = default(PageType);
        if (TargetLoginType != default)
        {
            if (TargetLoginType == ModLaunch.McLoginType.Ms)
                Type = PageType.Ms;
            if (TargetLoginType == ModLaunch.McLoginType.Auth)
                Type = PageType.Auth;
            if (TargetLoginType == ModLaunch.McLoginType.Legacy)
                Type = PageType.Offline;
        }
        else if (ModProfile.SelectedProfile is not null)
        {
            Type = PageType.ProfileSkin;
            BtnLaunch.IsEnabled = true;
        }
        else
        {
            Type = PageType.Profile;
            if (!(BtnLaunch.Text == "下载游戏"))
                BtnLaunch.IsEnabled = false;
        }

        // 刷新页面
        if (PageCurrent == Type)
            return;
        PageChange(Type, Anim);
    }

    #endregion

    #region 皮肤

    // 正版皮肤
    public static ModLoader.LoaderTask<ModBase.EqualableList<string>, string> SkinMs = new("Loader Skin Ms", SkinMsLoad,
        SkinMsInput, ThreadPriority.AboveNormal);

    private static ModBase.EqualableList<string> SkinMsInput()
    {
        // 获取名称
        return new ModBase.EqualableList<string>
            { ModProfile.SelectedProfile.Username, ModProfile.SelectedProfile.Uuid };
    }

    private static void SkinMsLoad(ModLoader.LoaderTask<ModBase.EqualableList<string>, string> Data)
    {
        // 清空已有皮肤
        // 如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
        ModBase.RunInUi(() =>
        {
            if (ModMain.FrmLoginProfileSkin is not null && ModMain.FrmLoginProfileSkin.Skin is not null)
                ModMain.FrmLoginProfileSkin.Skin.Clear();
        });
        // 获取 Url
        var UserName = Data.Input[0];
        var Uuid = Data.Input[1];
        if (ModProfile.SelectedProfile is not null)
        {
            UserName = ModProfile.SelectedProfile.Username;
            Uuid = ModProfile.SelectedProfile.Uuid;
        }

        if (string.IsNullOrEmpty(UserName))
        {
            Data.Output = ModBase.PathImage + "Skins/" + ModMinecraft.McSkinSex(ModProfile.GetOfflineUuid(UserName)) +
                          ".png";
            ModBase.Log("[Minecraft] 获取微软正版皮肤失败，ID 为空");
            goto Finish;
        }

        try
        {
            var Result = ModMinecraft.McSkinGetAddress(Uuid, "Ms");
            if (Data.IsAborted)
                throw new ThreadInterruptedException("当前任务已取消：" + UserName);
            Result = ModMinecraft.McSkinDownload(Result);
            if (Data.IsAborted)
                throw new ThreadInterruptedException("当前任务已取消：" + UserName);
            Data.Output = Result;
        }
        catch (Exception ex)
        {
            if (ex.GetType().Name == "ThreadInterruptedException")
            {
                Data.Output = "";
                ModBase.Log("[Minecraft] 已取消皮肤获取：" + UserName);
                return;
            }

            if (ex.ToString().Contains("429"))
            {
                Data.Output = ModBase.PathImage + "Skins/" +
                              ModMinecraft.McSkinSex(ModProfile.GetOfflineUuid(UserName)) + ".png";
                ModBase.Log("[Minecraft] 获取正版皮肤失败（" + UserName + "）：获取皮肤太过频繁，请 5 分钟后再试！", ModBase.LogLevel.Hint);
            }
            else if (ex.ToString().Contains("未设置自定义皮肤"))
            {
                Data.Output = ModBase.PathImage + "Skins/" +
                              ModMinecraft.McSkinSex(ModProfile.GetOfflineUuid(UserName)) + ".png";
                ModBase.Log("[Minecraft] 用户未设置自定义皮肤，跳过皮肤加载");
            }
            else
            {
                Data.Output = ModBase.PathImage + "Skins/" +
                              ModMinecraft.McSkinSex(ModProfile.GetOfflineUuid(UserName)) + ".png";
                ModBase.Log(ex, "获取微软正版皮肤失败（" + UserName + "）", ModBase.LogLevel.Hint);
            }
        }

        Finish: ;

        // 刷新显示
        if (ModMain.FrmLoginProfileSkin is not null && ReferenceEquals(ModMain.FrmLoginProfileSkin.Skin.Loader, Data))
            ModBase.RunInUi(ModMain.FrmLoginProfileSkin.Skin.Load);
        else if (!Data.IsAborted) // 如果已经中断，Input 也被清空，就不会再次刷新
            Data.Input = null; // 清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
    }

    // 离线皮肤
    public static ModLoader.LoaderTask<ModBase.EqualableList<string>, string> SkinLegacy = new("Loader Skin Legacy",
        SkinLegacyLoad, SkinLegacyInput, ThreadPriority.AboveNormal);

    private static ModBase.EqualableList<string> SkinLegacyInput()
    {
        return new ModBase.EqualableList<string>
            { ModProfile.SelectedProfile.Username, ModProfile.SelectedProfile.Uuid };
    }

    private static void SkinLegacyLoad(ModLoader.LoaderTask<ModBase.EqualableList<string>, string> Data)
    {
        // 清空已有皮肤
        ModBase.RunInUi(() =>
        {
            if (ModMain.FrmLoginProfileSkin is not null && ModMain.FrmLoginProfileSkin.Skin is not null)
                ModMain.FrmLoginProfileSkin.Skin.Clear();
        });
        Data.Output = ModBase.PathImage + "Skins/" + ModMinecraft.McSkinSex(Data.Input[1]) + ".png";
        // 刷新显示
        if (ModMain.FrmLoginProfileSkin is not null && ReferenceEquals(ModMain.FrmLoginProfileSkin.Skin.Loader, Data))
            ModBase.RunInUi(() => ModMain.FrmLoginProfileSkin.Skin.Load());
        else if (!Data.IsAborted) // 如果已经中断，Input 也被清空，就不会再次刷新
            Data.Input = null; // 清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
    }

    // Authlib-Injector 皮肤
    public static ModLoader.LoaderTask<ModBase.EqualableList<string>, string> SkinAuth = new("Loader Skin Auth",
        SkinAuthLoad, SkinAuthInput, ThreadPriority.AboveNormal);

    private static ModBase.EqualableList<string> SkinAuthInput()
    {
        // 获取名称
        return new ModBase.EqualableList<string>
            { ModProfile.SelectedProfile.Username, ModProfile.SelectedProfile.Uuid };
    }

    private static void SkinAuthLoad(ModLoader.LoaderTask<ModBase.EqualableList<string>, string> Data)
    {
        // 清空已有皮肤
        // 如果在输入时清空皮肤，若输入内容一样则不会执行 Load 方法，导致皮肤不被加载
        ModBase.RunInUi(() =>
        {
            if (ModMain.FrmLoginProfileSkin is not null && ModMain.FrmLoginProfileSkin.Skin is not null)
                ModMain.FrmLoginProfileSkin.Skin.Clear();
        });
        // 获取 Url
        var UserName = Data.Input[0];
        var Uuid = Data.Input[1];
        if (string.IsNullOrEmpty(UserName))
        {
            Data.Output = ModBase.PathImage + "Skins/Steve.png";
            ModBase.Log("[Minecraft] 获取 Authlib-Injector 皮肤失败，ID 为空");
            goto Finish;
        }

        try
        {
            var Result = ModMinecraft.McSkinGetAddress(Uuid, "Auth");
            if (Data.IsAborted)
                throw new ThreadInterruptedException("当前任务已取消：" + UserName);
            Result = ModMinecraft.McSkinDownload(Result);
            if (Data.IsAborted)
                throw new ThreadInterruptedException("当前任务已取消：" + UserName);
            Data.Output = Result;
        }
        catch (Exception ex)
        {
            if (ex.GetType().Name == "ThreadInterruptedException")
            {
                Data.Output = "";
                return;
            }

            if (ex.ToString().Contains("429"))
            {
                Data.Output = ModBase.PathImage + "Skins/Steve.png";
                ModBase.Log("[Minecraft] 获取 Authlib-Injector 皮肤失败（" + UserName + "）：获取皮肤太过频繁，请 5 分钟后再试！",
                    ModBase.LogLevel.Hint);
            }
            else if (ex.ToString().Contains("未设置自定义皮肤"))
            {
                Data.Output = ModBase.PathImage + "Skins/Steve.png";
                ModBase.Log("[Minecraft] 用户未设置自定义皮肤，跳过皮肤加载");
            }
            else
            {
                Data.Output = ModBase.PathImage + "Skins/Steve.png";
                ModBase.Log(ex, "获取 Authlib-Injector 皮肤失败（" + UserName + "）", ModBase.LogLevel.Hint);
            }
        }

        Finish: ;

        // 刷新显示
        if (ModMain.FrmLoginProfileSkin is not null && ReferenceEquals(ModMain.FrmLoginProfileSkin.Skin.Loader, Data))
            ModBase.RunInUi(ModMain.FrmLoginProfileSkin.Skin.Load);
        else if (!Data.IsAborted) // 如果已经中断，Input 也被清空，就不会再次刷新
            Data.Input = null; // 清空输入，因为皮肤实际上没有被渲染，如果不清空切换到页面的 Start 会由于输入相同而不渲染
    }

    // 全部皮肤加载器
    // 需要放在其中元素的后面，否则会因为它提前被加载而莫名其妙变成 Nothing
    public static List<ModLoader.LoaderTask<ModBase.EqualableList<string>, string>> SkinLoaders = new()
        { SkinMs, SkinLegacy, SkinAuth };

    #endregion
}
