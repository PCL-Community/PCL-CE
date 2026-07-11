// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Hosting;

#pragma warning disable CA1822, CS0067

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupUpdate : MyPageRight, IRefreshableSettingsPage, ISettingsPageInteractionSource
{
    private const string ReleasesUrl = "https://github.com/MuXue1230-owo/PCL-N/releases";
    private const string UnsupportedMessage = "Avalonia 版本暂不支持在线检查与自动安装更新，请前往 GitHub Releases 手动查看新版本。";
    private string _latestReleaseUrl = ReleasesUrl;
    private bool _isInitializing = true;
    private bool _isRevertingChannel;
    private int _lastUpdateChannel;

    public PageSetupUpdate()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = PanBack;
        LauncherSettingsPageBinder.Attach(this, _ =>
            _lastUpdateChannel = Math.Max(0, UpdateChannelCombo.SelectedIndex));
        _isInitializing = false;
        AttachedToVisualTree += (_, _) => RefreshPage();
        RefreshPage();
    }

    public event EventHandler<SettingsPathRequestedEventArgs>? OpenPathRequested;

    public event EventHandler<SettingsUrlRequestedEventArgs>? OpenUrlRequested;

    public event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;

    public void RefreshPage()
    {
        SetCurrentVersionText();
        SetUnsupportedState();
    }

    private void BtnChangelogDetail_Click(object? sender, RoutedEventArgs e)
    {
        OpenUrlRequested?.Invoke(this, new SettingsUrlRequestedEventArgs(_latestReleaseUrl));
    }

    private void BtnChangelog_Click(object? sender, EventArgs e)
    {
        OpenUrlRequested?.Invoke(this, new SettingsUrlRequestedEventArgs(_latestReleaseUrl));
    }

    private void BtnCheckAgain_OnClick(object? sender, EventArgs e)
    {
        ShowUnsupportedMessage();
    }

    private void BtnUpdate_Click(object? sender, EventArgs e)
    {
        ShowUnsupportedMessage();
    }

    private void ComboSystemUpdateBranch_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        MyComboBox combo = UpdateChannelCombo;
        if (_isInitializing || _isRevertingChannel || combo.SelectedIndex < 0)
            return;

        int selectedIndex = combo.SelectedIndex;
        if (selectedIndex == 0)
        {
            _lastUpdateChannel = 0;
            RefreshPage();
            return;
        }

        int previousIndex = _lastUpdateChannel;
        void Complete(bool confirmed)
        {
            if (confirmed)
            {
                _lastUpdateChannel = selectedIndex;
                RefreshPage();
                return;
            }

            _isRevertingChannel = true;
            try
            {
                combo.SelectedIndex = Math.Clamp(previousIndex, 0, combo.ItemCount - 1);
            }
            finally
            {
                _isRevertingChannel = false;
            }
        }

        string channel = selectedIndex == 1 ? "测试版" : "开发版";
        SettingsConfirmRequestedEventArgs args = new(
            "切换更新通道",
            $"{channel}可能包含尚未充分验证的功能和兼容性问题。确定切换到{channel}吗？",
            Complete,
            primaryButton: "仍然切换",
            isWarn: true);
        if (ConfirmRequested is { } confirmRequested)
            confirmRequested.Invoke(this, args);
        else
            Complete(false);
    }

    private void ComboSystemUpdateMode_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
    }

    private void SetCurrentVersionText()
    {
        string version = "PCL N " + PclMetadata.Current.DisplayVersion;
        if (this.FindControl<TextBlock>("TextCurrentVersion") is { } currentVersion)
            currentVersion.Text = version;
        if (this.FindControl<TextBlock>("TextUpdateName") is { } updateName)
            updateName.Text = version;
        if (this.FindControl<TextBlock>("TextCurrentDesc") is { } currentDescription)
            currentDescription.Text = "当前版本 · 暂不支持在线检查更新";
    }

    private void SetUnsupportedState()
    {
        if (this.FindControl<MyCard>("CardUpdate") is { } updateCard)
            updateCard.IsVisible = false;
        if (this.FindControl<MyCard>("CardCheck") is { } checkCard)
            checkCard.IsVisible = true;
        if (this.FindControl<MyButton>("BtnCheckAgain") is { } checkAgain)
            checkAgain.IsEnabled = true;
    }

    private void ShowUnsupportedMessage()
    {
        MessageRequested?.Invoke(
            this,
            new SettingsMessageRequestedEventArgs("暂不支持检查更新", UnsupportedMessage, "知道了"));
    }

    private MyComboBox UpdateChannelCombo => this.FindControl<MyComboBox>("ComboSystemUpdateChannel")
        ?? throw new InvalidOperationException("PageSetupUpdate 缺少 ComboSystemUpdateChannel。");

}
