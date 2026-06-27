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

    public IReadOnlyList<LaunchInstanceInfo> Instances { get; private set; } = [];

    public LaunchInstanceInfo? SelectedInstance { get; private set; }

    public Control? CurrentLoginPage { get; private set; }

    public event EventHandler? InstanceSelectRequested;

    public event EventHandler? InstanceSettingsRequested;

    public event EventHandler? DownloadRequested;

    public event EventHandler<LaunchInstanceInfo>? LaunchRequested;

    public event EventHandler? CancelLaunchRequested;

    public event EventHandler<string>? StatusMessage;

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
            RefreshButtonsUI();
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
        RefreshButtonsUI();
    }

    public void Dispose()
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = null;
        GC.SuppressFinalize(this);
    }

    public void SetLoginPage(Control page, bool animate)
    {
        Grid? panLogin = this.FindControl<Grid>("PanLogin");
        if (panLogin is null)
            return;

        CurrentLoginPage = page;
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
            launching.IsVisible = false;
            launching.Opacity = 0d;
            launching.IsHitTestVisible = false;
        }
    }

    public void ShowLaunching(LaunchInstanceInfo? instance)
    {
        if (this.FindControl<Grid>("PanInput") is { } input)
        {
            input.IsHitTestVisible = false;
            input.Opacity = 0d;
            input.IsVisible = false;
        }

        if (this.FindControl<Grid>("PanLaunching") is { } launching)
        {
            launching.IsVisible = true;
            launching.Opacity = 1d;
            launching.IsHitTestVisible = true;
        }

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

    public void RefreshButtonsUI()
    {
        if (SelectedInstance is null)
        {
            _launchButtonAction = LaunchButtonAction.Download;
            SetLaunchButton("下载游戏", isEnabled: true);
            SetText("LabVersion", "未找到可启动的游戏版本");
            SetButtonEnabled("BtnInstance", true);
            SetVisible("BtnMore", false);
            SetLoginSummary("尚未选择账户档案", "你可以先登录或创建离线档案；没有本地版本时会引导下载游戏。");
            return;
        }

        _launchButtonAction = LaunchButtonAction.Launch;
        SetLaunchButton("启动游戏", isEnabled: true);
        SetText("LabVersion", SelectedInstance.Name);
        SetButtonEnabled("BtnInstance", true);
        SetVisible("BtnMore", true);
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
        InstanceSelectRequested?.Invoke(this, EventArgs.Empty);
        _ = RefreshInstancesAsync();
    }

    private void BtnMore_Click(object? sender, EventArgs e)
    {
        InstanceSettingsRequested?.Invoke(this, EventArgs.Empty);
        if (SelectedInstance is not null)
            StatusMessage?.Invoke(this, $"当前版本位置：{SelectedInstance.InstanceDirectory}");
    }

    private void BtnLaunch_Click(object? sender, EventArgs e)
    {
        switch (_launchButtonAction)
        {
            case LaunchButtonAction.Launch when SelectedInstance is not null:
                ShowLaunching(SelectedInstance);
                LaunchRequested?.Invoke(this, SelectedInstance);
                break;
            case LaunchButtonAction.Download:
                DownloadRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        CancelLaunchRequested?.Invoke(this, EventArgs.Empty);
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
