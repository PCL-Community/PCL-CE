// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Hosting;
using PCL.Desktop.Localization;

namespace PCL.Desktop.Features.Launching.Views;

public partial class PageLaunchLeft : MyPageLeft, IDisposable
{
    private const int ProgressAnimDurationMs = 260;

    private LaunchButtonAction _launchButtonAction = LaunchButtonAction.Loading;
    private CancellationTokenSource? _refreshCancellation;
    private Task? _refreshInstancesTask;
    private bool _isLoadedOnce;
    private bool _isInstanceLoadFinished;
    private string? _minecraftRootDirectory;
    private string? _preferredInstanceDirectory;
    private double _showProgress;
    private double _targetProgress;
    private double _renderedProgress;
    private bool _showLaunchingHint = true;
    private StackPanel? _pluginInjectPanel;

    public PageLaunchLeft()
    {
        AvaloniaXamlLoader.Load(this);
        AnimatedControl = this.FindControl<Grid>("PanInput");
        WireLaunchButtonScaleMirror();
        RegisterPluginUiSurfaces();
        SetLoadingState();
        AttachedToVisualTree += (_, _) =>
        {
            if (_isLoadedOnce)
                return;

            _isLoadedOnce = true;
            _ = EnsureInstancesLoadedAsync();
        };
        DetachedFromVisualTree += (_, _) => UnregisterPluginUiSurfaces();
    }

    private void RegisterPluginUiSurfaces()
    {
        DesktopHostUiComposition.Instance.RegisterTarget("pcl.page.launch", this);
        if (this.FindControl<MyButton>("BtnLaunch") is { } launchButton)
            DesktopHostUiComposition.Instance.RegisterTarget("pcl.component.launch-button", launchButton);

        if (this.FindControl<Grid>("PanInput") is not { } input)
            return;

        _pluginInjectPanel = new StackPanel
        {
            Name = "PanPluginPrimaryActionsAfter",
            Spacing = 4,
            Margin = new Thickness(20, 6, 20, 0),
            Orientation = Orientation.Vertical
        };
        // Place after launch row (row 2): reuse row 3 area above instance buttons by inserting at end of grid.
        Grid.SetRow(_pluginInjectPanel, 3);
        Grid.SetColumn(_pluginInjectPanel, 0);
        Grid.SetColumnSpan(_pluginInjectPanel, 5);
        // Push inject host above instance buttons with negative margin if needed — keep simple append.
        input.Children.Add(_pluginInjectPanel);
        DesktopHostUiComposition.Instance.RegisterSlot(
            "pcl.page.launch",
            "primary-actions.after",
            _pluginInjectPanel);
    }

    private static void UnregisterPluginUiSurfaces()
    {
        DesktopHostUiComposition.Instance.UnregisterTarget("pcl.page.launch");
        DesktopHostUiComposition.Instance.UnregisterTarget("pcl.component.launch-button");
        DesktopHostUiComposition.Instance.UnregisterSlot("pcl.page.launch", "primary-actions.after");
    }

    private void WireLaunchButtonScaleMirror()
    {
        if (this.FindControl<MyButton>("BtnLaunch")?.RealRenderTransform is not ScaleTransform buttonScale ||
            this.FindControl<TextBlock>("LabVersion")?.RenderTransform is not ScaleTransform labelScale)
        {
            return;
        }

        labelScale.ScaleX = buttonScale.ScaleX;
        labelScale.ScaleY = buttonScale.ScaleY;
        buttonScale.GetObservable(ScaleTransform.ScaleXProperty).Subscribe(value => labelScale.ScaleX = value);
        buttonScale.GetObservable(ScaleTransform.ScaleYProperty).Subscribe(value => labelScale.ScaleY = value);
    }

    public interface ILoginPage
    {
        void Reload();
    }

    public enum LaunchButtonAction
    {
        Loading,
        Launch,
        Download,
        Disabled
    }

    public enum LaunchLoginPageType
    {
        None,
        Auth,
        Ms,
        Profile,
        ProfileSkin,
        Offline
    }

    public IReadOnlyList<LaunchInstanceInfo> Instances { get; private set; } = [];

    public LaunchInstanceInfo? SelectedInstance { get; private set; }

    public string? PreferredInstanceDirectory => _preferredInstanceDirectory;

    public string? MinecraftRootDirectory => _minecraftRootDirectory;

    public Control? CurrentLoginPage { get; private set; }

    public LaunchLoginPageType CurrentLoginPageType { get; private set; } = LaunchLoginPageType.None;

    public bool HasSelectedProfile { get; private set; }

    public bool IsLaunchInProgress { get; private set; }

    public double DisplayedLaunchProgress => _showProgress;

    public Func<bool>? CanLaunchByPageState { get; set; }

    public event EventHandler? InstanceSelectRequested;

    public event EventHandler? InstanceSettingsRequested;

    public event EventHandler? DownloadRequested;

    public event EventHandler<LaunchInstanceInfo>? LaunchRequested;

    public event EventHandler? CancelLaunchRequested;

    public event EventHandler<string>? StatusMessage;

    public event EventHandler<LaunchLoginPageType>? LoginPageRequested;

    public Task EnsureInstancesLoadedAsync()
    {
        if (_isInstanceLoadFinished)
            return Task.CompletedTask;
        if (_refreshInstancesTask is { IsCompleted: false })
            return _refreshInstancesTask;

        _refreshInstancesTask = RefreshInstancesAsync();
        return _refreshInstancesTask;
    }

    public async Task RefreshInstancesAsync()
    {
        string? selectedDirectory = NormalizeInstanceDirectory(SelectedInstance?.InstanceDirectory)
                                    ?? _preferredInstanceDirectory;
        IReadOnlyList<LaunchInstanceInfo> previousInstances = Instances;
        LaunchInstanceInfo? previousSelected = SelectedInstance;
        bool hadInstances = _isInstanceLoadFinished && previousInstances.Count > 0;

        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = new CancellationTokenSource();
        // Soft timeout so huge folders don't leave the button on “正在检查游戏版本” forever.
        _refreshCancellation.CancelAfter(TimeSpan.FromSeconds(20));
        CancellationToken cancellationToken = _refreshCancellation.Token;

        // Only hard-disable the launch button on first load; re-scan keeps previous UI.
        if (!hadInstances)
            await RunOnUiThreadAsync(SetLoadingState).ConfigureAwait(false);
        else
            await RunOnUiThreadAsync(() => StatusMessage?.Invoke(this, "正在刷新游戏版本列表…")).ConfigureAwait(false);

        try
        {
            Progress<LaunchInstanceDiscoveryProgress> progress = new(UpdateInstanceDiscoveryProgress);
            // Prefer the selected Minecraft root to avoid multi-folder full scans.
            IReadOnlyList<string> roots = !string.IsNullOrWhiteSpace(_minecraftRootDirectory)
                ? [_minecraftRootDirectory]
                : LaunchInstanceDiscovery.GetCandidateRoots();
            IReadOnlyList<LaunchInstanceInfo> instances = await LaunchInstanceDiscovery.DiscoverAsync(
                    roots,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested && instances.Count == 0 && hadInstances)
            {
                await RunOnUiThreadAsync(() =>
                {
                    _isInstanceLoadFinished = true;
                    RefreshButtonsUI();
                }).ConfigureAwait(false);
                return;
            }

            await RunOnUiThreadAsync(() =>
            {
                Instances = instances;
                SelectedInstance = FindInstanceByDirectory(Instances, selectedDirectory)
                                   ?? (Instances.Count > 0 ? Instances[0] : null);
                RememberSelectedInstance();
                _isInstanceLoadFinished = true;
                RefreshButtonsUI();
                RefreshPage(anim: false);
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Never leave the button stuck on “正在检查游戏版本”.
            await RunOnUiThreadAsync(() =>
            {
                if (!_isInstanceLoadFinished)
                {
                    Instances = previousInstances;
                    SelectedInstance = previousSelected;
                    _isInstanceLoadFinished = true;
                }

                RefreshButtonsUI();
                if (!hadInstances)
                    StatusMessage?.Invoke(this, "检查游戏版本已取消或超时。");
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiThreadAsync(() =>
            {
                if (hadInstances)
                {
                    Instances = previousInstances;
                    SelectedInstance = previousSelected;
                    _isInstanceLoadFinished = true;
                    RefreshButtonsUI();
                    StatusMessage?.Invoke(this, "刷新游戏版本失败：" + ex.Message);
                }
                else
                {
                    Instances = [];
                    SelectedInstance = null;
                    SetDisabledState("检查游戏版本时遇到问题");
                    StatusMessage?.Invoke(this, "未能检查本地游戏版本：" + ex.Message);
                }
            }).ConfigureAwait(false);
        }
    }

    public void SetInstances(IReadOnlyList<LaunchInstanceInfo> instances, LaunchInstanceInfo? selectedInstance = null)
    {
        string? selectedDirectory = NormalizeInstanceDirectory(selectedInstance?.InstanceDirectory)
                                    ?? NormalizeInstanceDirectory(SelectedInstance?.InstanceDirectory)
                                    ?? _preferredInstanceDirectory;
        Instances = instances;
        SelectedInstance = FindInstanceByDirectory(instances, selectedDirectory)
                           ?? (instances.Count > 0 ? instances[0] : null);
        RememberSelectedInstance();
        _isInstanceLoadFinished = true;
        RefreshButtonsUI();
    }

    public void SetPreferredInstanceDirectory(string? instanceDirectory)
    {
        _preferredInstanceDirectory = NormalizeInstanceDirectory(instanceDirectory);
        if (!_isInstanceLoadFinished || Instances.Count == 0)
            return;

        LaunchInstanceInfo? preferred = FindInstanceByDirectory(Instances, _preferredInstanceDirectory);
        if (preferred is null)
            return;

        SelectedInstance = preferred;
        RememberSelectedInstance();
        RefreshButtonsUI();
        RefreshPage(anim: false);
    }

    public void SetMinecraftRootDirectory(string? minecraftRootDirectory)
    {
        string? normalized = NormalizeMinecraftRoot(minecraftRootDirectory);
        if (string.Equals(_minecraftRootDirectory, normalized, StringComparison.OrdinalIgnoreCase))
            return;

        _minecraftRootDirectory = normalized;
        // Root changed: allow a fresh scan, but keep prior instances visible until the new scan finishes.
        _isInstanceLoadFinished = false;
        _ = RefreshInstancesAsync();
    }

    public void SetInstanceLoading(bool isLoading)
    {
        _isInstanceLoadFinished = !isLoading;
        if (isLoading)
            SetLoadingState();
        else
            RefreshButtonsUI();
    }

    public void SetSelectedProfilePresent(bool hasSelectedProfile)
    {
        HasSelectedProfile = hasSelectedProfile;
        RefreshButtonsUI();
        RefreshPage(anim: false);
    }

    public void Dispose()
    {
        UnregisterPluginUiSurfaces();
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = null;
        GC.SuppressFinalize(this);
    }

    public void SetLoginPage(Control page, bool animate, LaunchLoginPageType pageType = LaunchLoginPageType.None)
    {
        Grid? panLogin = this.FindControl<Grid>("PanLogin");
        if (panLogin is null)
            return;

        ModAnimation.AniStop("FrmLogin PageChange");
        CurrentLoginPage = page;
        if (pageType != LaunchLoginPageType.None)
            CurrentLoginPageType = pageType;
        page.Opacity = 1d;
        if (page is ILoginPage loginPage)
            loginPage.Reload();

        if (!animate)
        {
            panLogin.Children.Clear();
            panLogin.Children.Add(page);
            panLogin.Opacity = 1d;
            return;
        }

        ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaOpacity(
                    panLogin,
                    -panLogin.Opacity,
                    100,
                    ease: new ModAnimation.AniEaseOutFluent()),
                ModAnimation.AaCode(() =>
                {
                    panLogin.Children.Clear();
                    panLogin.Children.Add(page);
                }, 100),
                ModAnimation.AaOpacity(
                    panLogin,
                    1d,
                    100,
                    120,
                    new ModAnimation.AniEaseInFluent())
            },
            "FrmLogin PageChange");
    }

    public void PageChangeToLogin()
    {
        if (CurrentLoginPage is ILoginPage loginPage)
            loginPage.Reload();

        Grid? input = this.FindControl<Grid>("PanInput");
        Grid? launching = this.FindControl<Grid>("PanLaunching");
        if (input is null || launching is null)
            return;

        input.IsHitTestVisible = false;
        launching.IsHitTestVisible = false;
        SetLaunchLoadingState(MyLoading.MyLoadingState.Stop);
        input.IsVisible = true;
        ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaOpacity(launching, -launching.Opacity, 150),
                ModAnimation.AaScaleTransform(
                    launching,
                    0.8d - GetScaleX(launching),
                    150,
                    ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaOpacity(input, 1d - input.Opacity, 250, 50),
                ModAnimation.AaScaleTransform(
                    input,
                    1d - GetScaleX(input),
                    300,
                    50,
                    new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaCode(() => input.IsHitTestVisible = true, 200)
            },
            "Launch State Page",
            refreshTime: true);

        IsLaunchInProgress = false;
        SetVisible("PanLaunchingHint", false);
    }

    public void ConfigureLaunchingHint(bool isEnabled)
    {
        _showLaunchingHint = isEnabled;
        SetVisible("PanLaunchingHint", isEnabled && IsLaunchInProgress);
    }

    public void ShowLaunching(LaunchInstanceInfo? instance)
    {
        Grid? input = this.FindControl<Grid>("PanInput");
        Grid? launching = this.FindControl<Grid>("PanLaunching");
        if (input is null || launching is null)
            return;

        IsLaunchInProgress = true;
        _showProgress = 0d;
        _targetProgress = 0d;
        _renderedProgress = 0d;
        SetText("LabLaunchingTitle", AvaloniaLocalizationManager.GetText("Launch.Status.Title.Launching", "正在启动"));
        SetText("LabLaunchingName", instance?.Name ?? "等待选择版本");
        SetText("LabLaunchingStage", AvaloniaLocalizationManager.GetText("Common.Action.Initialize", "初始化"));
        SetText("LabLaunchingMethod", "等待账户档案");
        SetText("LabLaunchingHint", PageLaunchRight.GetRandomHint(enableLengthLimit: true, raw: true));
        SetVisible("PanLaunchingHint", _showLaunchingHint);
        ApplyLaunchProgressVisual(0d, animate: false);
        SetVisible("LabLaunchingDownloadLeft", false);
        SetVisible("LabLaunchingDownload", false);

        input.IsHitTestVisible = false;
        launching.IsHitTestVisible = false;
        SetLaunchLoadingState(MyLoading.MyLoadingState.Run);
        launching.IsVisible = true;
        ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaOpacity(input, 0d, 50),
                ModAnimation.AaOpacity(
                    input,
                    -input.Opacity,
                    110,
                    ease: new ModAnimation.AniEaseInFluent(),
                    after: true),
                ModAnimation.AaScaleTransform(input, 1.2d - GetScaleX(input), 160),
                ModAnimation.AaOpacity(launching, 1d - launching.Opacity, 150, 100),
                ModAnimation.AaScaleTransform(
                    launching,
                    1d - GetScaleX(launching),
                    500,
                    100,
                    new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaCode(() => launching.IsHitTestVisible = true, 150)
            },
            "Launch State Page");
    }

    public void ShowRepairing()
    {
        SetText("LabLaunchingTitle", "正在自动修复");
        SetText("LabLaunchingStage", "正在下载缺失文件");
        _targetProgress = 0d;
        _showProgress = 0d;
        ApplyLaunchProgressVisual(0d, animate: false);
    }

    public void UpdateRepairStep(int current, int total)
    {
        if (total <= 0)
            return;

        double ratio = Math.Clamp(current / (double)total, 0d, 1d);
        SetText("LabLaunchingStage", $"正在下载缺失文件 ({current}/{total})");
        _targetProgress = ratio;
        _showProgress = ratio;
        ApplyLaunchProgressVisual(ratio, animate: true);
    }

    public void HideRepairing()
    {
        SetText("LabLaunchingTitle", AvaloniaLocalizationManager.GetText("Launch.Status.Title.Launching", "正在启动"));
        SetText("LabLaunchingStage", AvaloniaLocalizationManager.GetText("Common.Action.Initialize", "初始化"));
        _targetProgress = 0d;
        _showProgress = 0d;
        ApplyLaunchProgressVisual(0d, animate: false);
    }

    public void UpdateLaunchingStatus(string stage, double progress, string? method = null)
    {
        LaunchingRefresh(stage, progress, isLaunched: false, method);
    }

    public void LaunchingRefresh(
        string stage,
        double actualProgress,
        bool isLaunched = false,
        string? method = null,
        string? downloadSpeed = null)
    {
        actualProgress = Math.Clamp(actualProgress, 0d, 1d);
        _targetProgress = isLaunched ? 1d : actualProgress;

        // WPF-style ease: each refresh (including stage heartbeats) creeps display progress
        // toward the real value, then animates the bar over ~260ms so it never looks frozen.
        if (isLaunched)
            _showProgress = 1d;
        else if (actualProgress < _showProgress)
            _showProgress = actualProgress;
        else
        {
            _showProgress += (_targetProgress - _showProgress) * 0.2d + 0.005d;
            if (_showProgress > _targetProgress)
                _showProgress = _targetProgress;
        }

        _showProgress = Math.Clamp(_showProgress, 0d, 1d);
        ApplyLaunchProgressVisual(_showProgress, animate: true);

        SetText(
            "LabLaunchingTitle",
            isLaunched
                ? AvaloniaLocalizationManager.GetText("Launch.Status.Title.Launched", "游戏已启动")
                : AvaloniaLocalizationManager.GetText("Launch.Status.Title.Launching", "正在启动"));
        SetText("LabLaunchingStage", stage);
        if (!string.IsNullOrWhiteSpace(method))
            SetText("LabLaunchingMethod", method);

        bool hasDownloadSpeed = !string.IsNullOrWhiteSpace(downloadSpeed);
        SetVisible("LabLaunchingDownloadLeft", hasDownloadSpeed);
        SetVisible("LabLaunchingDownload", hasDownloadSpeed);
        if (hasDownloadSpeed)
        {
            SetOpacity("LabLaunchingDownloadLeft", 1d);
            SetOpacity("LabLaunchingDownload", 1d);
            SetText("LabLaunchingDownload", downloadSpeed!);
        }
    }

    public void RefreshButtonsUI()
    {
        if (!_isInstanceLoadFinished)
        {
            SetLoadingState();
            return;
        }

        SetInstanceCheckLoadingState(MyLoading.MyLoadingState.Stop, isVisible: false);

        if (SelectedInstance is null)
        {
            _launchButtonAction = LaunchButtonAction.Download;
            SetLaunchButton("下载游戏", isEnabled: true);

            SetText("LabVersion", "未找到可启动的游戏版本");
            SetButtonEnabled("BtnInstance", true);
            SetVisible("BtnMore", false);
            SetLoginSummary("尚未选择账户档案", "你可以先登录或创建离线档案；没有本地版本时会引导下载游戏。");
            SetVisible("BtnInstance", true);
            return;
        }

        _launchButtonAction = LaunchButtonAction.Launch;
        SetLaunchButton("启动游戏", isEnabled: HasSelectedProfile);
        SetText("LabVersion", SelectedInstance.Name);
        SetButtonEnabled("BtnInstance", true);
        SetVisible("BtnInstance", true);
        SetVisible("BtnMore", true);
        SetLoginSummary("账户档案入口已就绪", "Microsoft、第三方与离线档案会继续挂载到这里。");
    }

    private void SetLoadingState()
    {
        _isInstanceLoadFinished = false;
        _launchButtonAction = LaunchButtonAction.Loading;
        SetLaunchButton("正在加载", isEnabled: false);
        SetText("LabVersion", "正在检查游戏版本");
        SetButtonEnabled("BtnInstance", false);
        SetVisible("BtnMore", false);
        SetLoginSummary("正在读取账户档案", "Microsoft、第三方与离线档案页面会继续沿用这里的分页入口。");
        SetInstanceCheckLoadingState(MyLoading.MyLoadingState.Run, isVisible: true);
    }

    private void SetDisabledState(string message)
    {
        _isInstanceLoadFinished = true;
        _launchButtonAction = LaunchButtonAction.Disabled;
        SetLaunchButton("启动游戏", isEnabled: false);
        SetText("LabVersion", message);
        SetButtonEnabled("BtnInstance", true);
        SetInstanceCheckLoadingState(MyLoading.MyLoadingState.Error, isVisible: true);
        SetVisible("BtnMore", false);
    }

    private void BtnInstance_Click(object? sender, EventArgs e)
    {
        if (IsLaunchInProgress)
            return;

        InstanceSelectRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BtnMore_Click(object? sender, EventArgs e)
    {
        if (IsLaunchInProgress || SelectedInstance is null)
            return;

        if (HasIgnoreMarker(SelectedInstance))
        {
            StatusMessage?.Invoke(this, "该版本仍在安装中，暂时不能调整设置。");
            return;
        }

        InstanceSettingsRequested?.Invoke(this, EventArgs.Empty);
        StatusMessage?.Invoke(this, $"当前版本位置：{SelectedInstance.InstanceDirectory}");
    }

    public void LaunchButtonClick()
    {
        if (IsLaunchInProgress ||
            this.FindControl<MyButton>("BtnLaunch") is not { IsEnabled: true } ||
            CanLaunchByPageState?.Invoke() == false)
        {
            return;
        }

        switch (_launchButtonAction)
        {
            case LaunchButtonAction.Launch when SelectedInstance is not null:
                if (HasIgnoreMarker(SelectedInstance))
                {
                    StatusMessage?.Invoke(this, "该版本仍在安装中，暂时不能启动。");
                    return;
                }

                // Paint launching UI first; fire LaunchRequested on next dispatcher pass so
                // the click stack returns and animations can start before any launch work.
                LaunchInstanceInfo instance = SelectedInstance;
                ShowLaunching(instance);
                Dispatcher.UIThread.Post(
                    () => LaunchRequested?.Invoke(this, instance),
                    DispatcherPriority.Background);
                break;
            case LaunchButtonAction.Download:
                DownloadRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    public void RefreshPage(bool anim, LaunchLoginPageType targetLoginType = LaunchLoginPageType.None)
    {
        LaunchLoginPageType type = targetLoginType;
        if (type == LaunchLoginPageType.None)
            type = HasSelectedProfile ? LaunchLoginPageType.ProfileSkin : LaunchLoginPageType.Profile;

        if (CurrentLoginPageType == type)
            return;

        CurrentLoginPageType = type;
        LoginPageRequested?.Invoke(this, type);
        ApplyLoginStateSummary(type);
        if (!HasSelectedProfile && _launchButtonAction != LaunchButtonAction.Download)
            SetLaunchButton("启动游戏", isEnabled: false);
    }

    private void BtnLaunch_Click(object? sender, EventArgs e) => LaunchButtonClick();

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        if (!IsLaunchInProgress)
            return;

        SetText("LabLaunchingStage", AvaloniaLocalizationManager.GetText(
            "Minecraft.Launch.Cancelled.Request",
            "已请求取消启动"));
        // Request cancellation first; return to login immediately so the launching pane
        // does not stay stuck if the orchestrator is mid-stage. MainWindow only cancels
        // the token and does not call PageChangeToLogin again.
        CancelLaunchRequested?.Invoke(this, EventArgs.Empty);
        if (IsLaunchInProgress)
            PageChangeToLogin();
    }

    private void PanLaunchingInfo_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
    }

    private void SetLaunchButton(string text, bool isEnabled)
    {
        if (this.FindControl<MyButton>("BtnLaunch") is { } button)
        {
            button.Text = text;
            button.IsEnabled = isEnabled;
        }
    }

    private void SetButtonEnabled(string name, bool isEnabled)
    {
        if (this.FindControl<Control>(name) is { } control)
            control.IsEnabled = isEnabled;
    }

    private void SetVisible(string name, bool isVisible)
    {
        if (this.FindControl<Control>(name) is { } control)
            control.IsVisible = isVisible;
    }

    private void SetOpacity(string name, double opacity)
    {
        if (this.FindControl<Control>(name) is { } control)
            control.Opacity = opacity;
    }

    private void ApplyLoginStateSummary(LaunchLoginPageType type)
    {
        string title = type switch
        {
            LaunchLoginPageType.Auth => "第三方登录",
            LaunchLoginPageType.Ms => "Microsoft 登录",
            LaunchLoginPageType.ProfileSkin => "账户档案",
            LaunchLoginPageType.Offline => "离线档案",
            _ => "选择账户档案"
        };
        string subtitle = type switch
        {
            LaunchLoginPageType.ProfileSkin => "已选择账户档案，可以启动已安装的游戏版本。",
            LaunchLoginPageType.Profile => "请选择或创建一个账户档案，之后才能启动游戏。",
            LaunchLoginPageType.Ms => "使用 Microsoft 账户登录并创建在线档案。",
            LaunchLoginPageType.Auth => "使用第三方认证服务器登录并创建在线档案。",
            LaunchLoginPageType.Offline => "创建仅保存在本机的离线档案。",
            _ => "请选择一个账户档案。"
        };
        SetLoginSummary(title, subtitle);
    }

    private static bool HasIgnoreMarker(LaunchInstanceInfo instance) =>
        File.Exists(instance.InstanceDirectory + ".pclignore");

    private static LaunchInstanceInfo? FindInstanceByDirectory(
        IReadOnlyList<LaunchInstanceInfo> instances,
        string? instanceDirectory)
    {
        if (string.IsNullOrWhiteSpace(instanceDirectory))
            return null;

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        foreach (LaunchInstanceInfo instance in instances)
        {
            string? candidate = NormalizeInstanceDirectory(instance.InstanceDirectory);
            if (candidate is not null && string.Equals(candidate, instanceDirectory, comparison))
                return instance;
        }

        return null;
    }

    private void RememberSelectedInstance()
    {
        if (SelectedInstance is not null)
            _preferredInstanceDirectory = NormalizeInstanceDirectory(SelectedInstance.InstanceDirectory);
    }

    private static string? NormalizeInstanceDirectory(string? instanceDirectory)
    {
        if (string.IsNullOrWhiteSpace(instanceDirectory))
            return null;

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(instanceDirectory.Trim()));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? NormalizeMinecraftRoot(string? minecraftRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(minecraftRootDirectory))
            return null;

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(minecraftRootDirectory.Trim()));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private void SetLoginSummary(string title, string subtitle)
    {
        SetText("LabLoginTitle", title);
        SetText("LabLoginSubtitle", subtitle);
    }

    private void SetText(string name, string text)
    {
        if (this.FindControl<TextBlock>(name) is { } block)
            block.Text = text;
    }

    private void ApplyLaunchProgressVisual(double ratio, bool animate)
    {
        ratio = Math.Clamp(ratio, 0d, 1d);
        SetText("LabLaunchingProgress", ratio.ToString("P2", System.Globalization.CultureInfo.CurrentCulture));

        if (this.FindControl<Grid>("PanLaunchingProgressBar") is not { ColumnDefinitions.Count: >= 2 } progressBar)
        {
            _renderedProgress = ratio;
            return;
        }

        ColumnDefinition finished = progressBar.ColumnDefinitions[0];
        ColumnDefinition unfinished = progressBar.ColumnDefinitions[1];
        if (!animate)
        {
            ModAnimation.AniStop("Launching Progress");
            finished.Width = new GridLength(ratio, GridUnitType.Star);
            unfinished.Width = new GridLength(1d - ratio, GridUnitType.Star);
            _renderedProgress = ratio;
            return;
        }

        double finishedDelta = ratio - finished.Width.Value;
        double unfinishedDelta = (1d - ratio) - unfinished.Width.Value;
        if (Math.Abs(finishedDelta) < 0.0005d && Math.Abs(unfinishedDelta) < 0.0005d)
        {
            _renderedProgress = ratio;
            return;
        }

        ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaGridLengthWidth(
                    finished,
                    finishedDelta,
                    ProgressAnimDurationMs,
                    ease: new ModAnimation.AniEaseOutFluent()),
                ModAnimation.AaGridLengthWidth(
                    unfinished,
                    unfinishedDelta,
                    ProgressAnimDurationMs,
                    ease: new ModAnimation.AniEaseOutFluent())
            },
            "Launching Progress");
        _renderedProgress = ratio;
    }

    private void SetLaunchLoadingState(MyLoading.MyLoadingState state)
    {
        if (this.FindControl<MyLoading>("LoadLaunching") is { } loading)
            loading.State.LoadingState = state;
    }

    private void UpdateInstanceDiscoveryProgress(LaunchInstanceDiscoveryProgress progress)
    {
        string detail = progress.Total > 0 && progress.Stage == "正在检查游戏版本"
            ? $"{progress.Stage} ({Math.Min(progress.Current, progress.Total)}/{progress.Total}) · 已找到 {progress.Found} 个"
            : progress.Stage;
        SetText("LabVersion", detail);
        if (this.FindControl<MyLoading>("LoadInstanceCheck") is { } loading)
            loading.Text = detail;
    }

    private void SetInstanceCheckLoadingState(MyLoading.MyLoadingState state, bool isVisible)
    {
        if (this.FindControl<MyLoading>("LoadInstanceCheck") is not { } loading)
            return;

        loading.IsVisible = isVisible;
        loading.State.LoadingState = state;
        if (state == MyLoading.MyLoadingState.Run)
            loading.Text = "正在检查游戏版本";
    }

    private static double GetScaleX(Control control) =>
        control.RenderTransform is ScaleTransform scale ? scale.ScaleX : 1d;

    private static Task RunOnUiThreadAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return Dispatcher.UIThread.InvokeAsync(action).GetTask();
    }
}
