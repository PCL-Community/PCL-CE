// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Net.Http.Headers;
using System.Text.Json;
using PCL.Desktop.Controls.Legacy;

#pragma warning disable CA1822, CS0067

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupUpdate : MyPageRight, IRefreshableSettingsPage, ISettingsPageInteractionSource
{
    private static readonly Uri LatestReleaseApiUri = new("https://api.github.com/repos/MuXue1230-owo/PCL-N/releases/latest");
    private const string ReleasesUrl = "https://github.com/MuXue1230-owo/PCL-N/releases";
    private string _latestReleaseUrl = ReleasesUrl;
    private bool _isInitializing = true;
    private bool _isRevertingChannel;
    private int _lastUpdateChannel;
    private CancellationTokenSource? _updateCheckCancellation;

    public PageSetupUpdate()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = PanBack;
        LauncherSettingsPageBinder.Attach(this, _ =>
            _lastUpdateChannel = Math.Max(0, UpdateChannelCombo.SelectedIndex));
        _isInitializing = false;
        AttachedToVisualTree += (_, _) => RefreshPage();
        DetachedFromVisualTree += (_, _) => _updateCheckCancellation?.Cancel();
    }

    public event EventHandler<SettingsPathRequestedEventArgs>? OpenPathRequested;

    public event EventHandler<SettingsUrlRequestedEventArgs>? OpenUrlRequested;

    public event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;

    public void RefreshPage()
    {
        SetCurrentVersionText();
        _ = RefreshLatestReleaseAsync();
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
        RefreshPage();
    }

    private void BtnUpdate_Click(object? sender, EventArgs e)
    {
        OpenUrlRequested?.Invoke(this, new SettingsUrlRequestedEventArgs(_latestReleaseUrl));
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

    private async Task RefreshLatestReleaseAsync()
    {
        _updateCheckCancellation?.Cancel();
        _updateCheckCancellation?.Dispose();
        _updateCheckCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _updateCheckCancellation.Token;
        SetCheckingState();
        try
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PCL-N", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            await using Stream stream = await client.GetStreamAsync(LatestReleaseApiUri, cancellationToken).ConfigureAwait(true);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(true);
            JsonElement root = document.RootElement;
            string tagName = root.TryGetProperty("tag_name", out JsonElement tag) ? tag.GetString() ?? "latest" : "latest";
            string releaseName = root.TryGetProperty("name", out JsonElement name) ? name.GetString() ?? tagName : tagName;
            string body = root.TryGetProperty("body", out JsonElement releaseBody) ? releaseBody.GetString() ?? string.Empty : string.Empty;
            _latestReleaseUrl = root.TryGetProperty("html_url", out JsonElement url)
                ? url.GetString() ?? ReleasesUrl
                : ReleasesUrl;

            if (TextUpdateName is not null)
                TextUpdateName.Text = "PCL N " + NormalizeVersionName(releaseName, tagName);
            if (TextUpdateDesc is not null)
                TextUpdateDesc.Text = "GitHub Releases";
            if (TextChangelog is not null)
                TextChangelog.Text = string.IsNullOrWhiteSpace(body)
                    ? "已获取到最新版本信息。点击右侧按钮可打开下载页面。"
                    : TrimChangelog(body);
            if (BtnUpdate is not null)
                BtnUpdate.Text = "打开";
            if (CardUpdate is not null)
                CardUpdate.IsVisible = true;
            if (CardCheck is not null)
                CardCheck.IsVisible = true;
            if (BtnCheckAgain is not null)
                BtnCheckAgain.IsEnabled = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
        {
            if (CardUpdate is not null)
                CardUpdate.IsVisible = false;
            if (CardCheck is not null)
                CardCheck.IsVisible = true;
            if (TextCurrentDesc is not null)
                TextCurrentDesc.Text = "检查更新失败";
            if (BtnCheckAgain is not null)
                BtnCheckAgain.IsEnabled = true;
            MessageRequested?.Invoke(
                this,
                new SettingsMessageRequestedEventArgs(
                    "检查更新失败",
                    "未能获取 GitHub Releases。请稍后重试，或手动打开 Releases 页面查看更新。\n\n详细信息：" + ex.Message));
        }
    }

    private void SetCurrentVersionText()
    {
        if (TextCurrentVersion is not null)
            TextCurrentVersion.Text = "PCL N " + PclBuildInfo.DisplayVersion;
        if (TextCurrentDesc is not null)
            TextCurrentDesc.Text = "当前版本";
    }

    private void SetCheckingState()
    {
        if (CardUpdate is not null)
            CardUpdate.IsVisible = false;
        if (CardCheck is not null)
            CardCheck.IsVisible = true;
        if (TextCurrentDesc is not null)
            TextCurrentDesc.Text = "正在检查更新...";
        if (BtnCheckAgain is not null)
            BtnCheckAgain.IsEnabled = false;
    }

    private MyComboBox UpdateChannelCombo => this.FindControl<MyComboBox>("ComboSystemUpdateChannel")
        ?? throw new InvalidOperationException("PageSetupUpdate 缺少 ComboSystemUpdateChannel。");

    private static string NormalizeVersionName(string releaseName, string tagName)
    {
        string value = string.IsNullOrWhiteSpace(releaseName) ? tagName : releaseName;
        return value.StartsWith("PCL N", StringComparison.OrdinalIgnoreCase) ? value["PCL N".Length..].Trim() : value;
    }

    private static string TrimChangelog(string text)
    {
        const int maxLength = 700;
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "\n...";
    }
}
