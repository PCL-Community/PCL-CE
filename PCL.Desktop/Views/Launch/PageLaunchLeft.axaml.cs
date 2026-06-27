// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Views.Launch;

public partial class PageLaunchLeft : MyPageLeft, IDisposable
{
    private LaunchButtonAction _launchButtonAction = LaunchButtonAction.Loading;
    private CancellationTokenSource? _refreshCancellation;
    private bool _isLoadedOnce;
    private bool _isInstanceLoadFinished;
    private double _showProgress;

    public PageLaunchLeft()
    {
        AvaloniaXamlLoader.Load(this);
        AnimatedControl = this.FindControl<Grid>("PanInput");
        AttachedToVisualTree += (_, _) =>
        {
            if (_isLoadedOnce)
                return;

            _isLoadedOnce = true;
            _ = RefreshInstancesAsync();
        };
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

    public Control? CurrentLoginPage { get; private set; }

    public LaunchLoginPageType CurrentLoginPageType { get; private set; } = LaunchLoginPageType.None;

    public bool HasSelectedProfile { get; private set; }

    public bool IsDownloadPageHidden { get; private set; }

    public bool IsFunctionSelectHidden { get; private set; }

    public bool HiddenForceShow { get; private set; }

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

    public async Task RefreshInstancesAsync()
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _refreshCancellation.Token;

        SetLoadingState();
        try
        {
            Instances = await LaunchInstanceDiscovery.DiscoverAsync(cancellationToken).ConfigureAwait(true);
            SelectedInstance = Instances.Count > 0 ? Instances[0] : null;
            _isInstanceLoadFinished = true;
            RefreshButtonsUI();
            RefreshPage(anim: false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Instances = [];
            SelectedInstance = null;
            SetDisabledState("检查游戏版本时遇到问题");
            StatusMessage?.Invoke(this, "未能检查本地游戏版本：" + ex.Message);
        }
    }

    public void SetInstances(IReadOnlyList<LaunchInstanceInfo> instances, LaunchInstanceInfo? selectedInstance = null)
    {
        Instances = instances;
        SelectedInstance = selectedInstance ?? (instances.Count > 0 ? instances[0] : null);
        _isInstanceLoadFinished = true;
        RefreshButtonsUI();
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

    public void SetPreferenceState(
        bool isDownloadPageHidden,
        bool isFunctionSelectHidden,
        bool hiddenForceShow = false)
    {
        IsDownloadPageHidden = isDownloadPageHidden;
        IsFunctionSelectHidden = isFunctionSelectHidden;
        HiddenForceShow = hiddenForceShow;
        RefreshButtonsUI();
    }

    public void Dispose()
    {
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

        CurrentLoginPage = page;
        if (pageType != LaunchLoginPageType.None)
            CurrentLoginPageType = pageType;
        panLogin.Children.Clear();
        panLogin.Children.Add(page);
        page.Opacity = 1d;
        if (page is ILoginPage loginPage)
            loginPage.Reload();
    }

    public void PageChangeToLogin()
    {
        if (CurrentLoginPage is ILoginPage loginPage)
            loginPage.Reload();

        if (this.FindControl<Grid>("PanInput") is { } input)
        {
            input.IsVisible = true;
            input.Opacity = 1d;
            input.IsHitTestVisible = true;
        }

        if (this.FindControl<Grid>("PanLaunching") is { } launching)
        {
            launching.Opacity = 0d;
            launching.IsHitTestVisible = false;
        }

        IsLaunchInProgress = false;
    }

    public void ShowLaunching(LaunchInstanceInfo? instance)
    {
        if (this.FindControl<Grid>("PanInput") is { } input)
        {
            input.IsHitTestVisible = false;
            input.Opacity = 0d;
            input.IsVisible = true;
        }

        if (this.FindControl<Grid>("PanLaunching") is { } launching)
        {
            launching.IsVisible = true;
            launching.Opacity = 1d;
            launching.IsHitTestVisible = true;
        }

        IsLaunchInProgress = true;
        _showProgress = 0d;
        SetText("LabLaunchingTitle", "正在启动");
        SetText("LabLaunchingName", instance?.Name ?? "等待选择版本");
        SetText("LabLaunchingStage", "准备启动环境");
        SetText("LabLaunchingMethod", "等待账户档案");
        SetLaunchProgress(0.05d);
    }

    public void ShowRepairing()
    {
        SetText("LabLaunchingTitle", "正在自动修复");
        SetText("LabLaunchingStage", "正在下载缺失文件");
        SetLaunchProgress(0d);
    }

    public void UpdateRepairStep(int current, int total)
    {
        if (total <= 0)
            return;

        double ratio = Math.Clamp(current / (double)total, 0d, 1d);
        SetText("LabLaunchingStage", $"正在下载缺失文件 ({current}/{total})");
        SetLaunchProgress(ratio);
    }

    public void HideRepairing()
    {
        SetText("LabLaunchingTitle", "正在启动");
        SetText("LabLaunchingStage", "初始化");
        SetLaunchProgress(0d);
    }

    public void UpdateLaunchingStatus(string stage, double progress, string? method = null)
    {
        SetText("LabLaunchingStage", stage);
        if (!string.IsNullOrWhiteSpace(method))
            SetText("LabLaunchingMethod", method);
        SetLaunchProgress(progress);
    }

    public void LaunchingRefresh(
        string stage,
        double actualProgress,
        bool isLaunched = false,
        string? method = null,
        string? downloadSpeed = null)
    {
        actualProgress = Math.Clamp(actualProgress, 0d, 1d);
        if (actualProgress >= _showProgress)
            _showProgress += (actualProgress - _showProgress) * 0.2d + 0.005d;
        if (actualProgress <= _showProgress)
            _showProgress = actualProgress;
        if (isLaunched)
            _showProgress = 1d;

        SetText("LabLaunchingTitle", isLaunched ? "游戏已启动" : "正在启动");
        SetText("LabLaunchingStage", stage);
        if (!string.IsNullOrWhiteSpace(method))
            SetText("LabLaunchingMethod", method);
        SetLaunchProgress(_showProgress);

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

        if (SelectedInstance is null)
        {
            if (IsDownloadPageHidden && !HiddenForceShow)
            {
                _launchButtonAction = LaunchButtonAction.Disabled;
                SetLaunchButton("启动游戏", isEnabled: false);
            }
            else
            {
                _launchButtonAction = LaunchButtonAction.Download;
                SetLaunchButton("下载游戏", isEnabled: true);
            }

            SetText("LabVersion", "未找到可启动的游戏版本");
            SetButtonEnabled("BtnInstance", true);
            SetVisible("BtnMore", false);
            SetLoginSummary("尚未选择账户档案", "你可以先登录或创建离线档案；没有本地版本时会引导下载游戏。");
            ApplyFunctionVisibility();
            return;
        }

        _launchButtonAction = LaunchButtonAction.Launch;
        SetLaunchButton("启动游戏", isEnabled: HasSelectedProfile);
        SetText("LabVersion", SelectedInstance.Name);
        SetButtonEnabled("BtnInstance", true);
        ApplyFunctionVisibility();
        SetLoginSummary("账户档案入口已就绪", "Microsoft、第三方与离线档案会继续挂载到这里。");
    }

    private void SetLoadingState()
    {
        _launchButtonAction = LaunchButtonAction.Loading;
        SetLaunchButton("正在加载", isEnabled: false);
        SetText("LabVersion", "正在检查游戏版本");
        SetButtonEnabled("BtnInstance", false);
        SetVisible("BtnMore", false);
        SetLoginSummary("正在读取账户档案", "Microsoft、第三方与离线档案页面会继续沿用这里的分页入口。");
    }

    private void SetDisabledState(string message)
    {
        _launchButtonAction = LaunchButtonAction.Disabled;
        SetLaunchButton("启动游戏", isEnabled: false);
        SetText("LabVersion", message);
        SetButtonEnabled("BtnInstance", true);
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

                ShowLaunching(SelectedInstance);
                LaunchRequested?.Invoke(this, SelectedInstance);
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
        ApplyLoginPlaceholder(type);
        if (!HasSelectedProfile && _launchButtonAction != LaunchButtonAction.Download)
            SetLaunchButton("启动游戏", isEnabled: false);
    }

    private void BtnLaunch_Click(object? sender, EventArgs e) => LaunchButtonClick();

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        if (!IsLaunchInProgress)
            return;

        CancelLaunchRequested?.Invoke(this, EventArgs.Empty);
        SetText("LabLaunchingStage", "已请求取消启动");
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

    private void ApplyFunctionVisibility()
    {
        bool visible = HiddenForceShow || !IsFunctionSelectHidden;
        SetVisible("BtnInstance", visible);
        SetVisible("BtnMore", SelectedInstance is not null && visible);
    }

    private void ApplyLoginPlaceholder(LaunchLoginPageType type)
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
            LaunchLoginPageType.Ms => "登录前会先展示必要的政策提示。",
            LaunchLoginPageType.Auth => "第三方认证服务会继续沿用这里的分页入口。",
            LaunchLoginPageType.Offline => "离线档案会继续沿用这里的分页入口。",
            _ => "账户分页正在迁移，会保持与 WPF 版本相同的切换入口。"
        };
        SetLoginSummary(title, subtitle);
    }

    private static bool HasIgnoreMarker(LaunchInstanceInfo instance) =>
        File.Exists(instance.InstanceDirectory + ".pclignore");

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

    private void SetLaunchProgress(double ratio)
    {
        ratio = Math.Clamp(ratio, 0d, 1d);
        if (this.FindControl<Grid>("PanLaunchingProgressBar") is { ColumnDefinitions.Count: >= 2 } progressBar)
        {
            progressBar.ColumnDefinitions[0].Width = new GridLength(ratio, GridUnitType.Star);
            progressBar.ColumnDefinitions[1].Width = new GridLength(1d - ratio, GridUnitType.Star);
        }

        SetText("LabLaunchingProgress", ratio.ToString("P0", System.Globalization.CultureInfo.CurrentCulture));
    }
}
