// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using PCL.Desktop.Controls.Legacy;

#pragma warning disable CA1822, CS0067

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupUpdate : MyPageRight, IRefreshableSettingsPage, ISettingsPageInteractionSource
{
    private static readonly Uri LatestReleaseApiUri = new("https://api.github.com/repos/MuXue1230-owo/PCL-N/releases/latest");
    private const string ReleasesUrl = "https://github.com/MuXue1230-owo/PCL-N/releases";
    private string _latestReleaseUrl = ReleasesUrl;

    public PageSetupUpdate()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = PanBack;
        LauncherSettingsPageBinder.Attach(this);
        AttachedToVisualTree += (_, _) => RefreshPage();
    }

    public event EventHandler<SettingsPathRequestedEventArgs>? OpenPathRequested;

    public event EventHandler<SettingsUrlRequestedEventArgs>? OpenUrlRequested;

    public event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;

    public async void RefreshPage()
    {
        SetCurrentVersionText();
        await RefreshLatestReleaseAsync().ConfigureAwait(true);
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
    }

    private void ComboSystemUpdateMode_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
    }

    private async Task RefreshLatestReleaseAsync()
    {
        try
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PCL-N", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            await using Stream stream = await client.GetStreamAsync(LatestReleaseApiUri).ConfigureAwait(true);
            using JsonDocument document = await JsonDocument.ParseAsync(stream).ConfigureAwait(true);
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
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
        {
            if (CardUpdate is not null)
                CardUpdate.IsVisible = false;
            if (CardCheck is not null)
                CardCheck.IsVisible = true;
            MessageRequested?.Invoke(
                this,
                new SettingsMessageRequestedEventArgs(
                    "检查更新失败",
                    "未能获取 GitHub Releases。请稍后重试，或手动打开 Releases 页面查看更新。\n\n详细信息：" + ex.Message));
        }
    }

    private void SetCurrentVersionText()
    {
        string version = typeof(PageSetupUpdate).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                         ?? typeof(PageSetupUpdate).Assembly.GetName().Version?.ToString()
                         ?? "dev";
        int metadataIndex = version.IndexOf('+', StringComparison.Ordinal);
        if (metadataIndex > 0)
            version = version[..metadataIndex];

        if (TextCurrentVersion is not null)
            TextCurrentVersion.Text = "PCL N " + version;
        if (TextCurrentDesc is not null)
            TextCurrentDesc.Text = "当前版本";
    }

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
