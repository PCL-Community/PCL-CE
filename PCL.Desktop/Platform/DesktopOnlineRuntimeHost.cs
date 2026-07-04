// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;
using System.Globalization;
using Avalonia.Threading;
using PCL.Application.Settings;
using PCL.Online;
using PCL.Platform.Paths;

namespace PCL.Desktop.Platform;

internal sealed class DesktopOnlineRuntimeHost :
    IOnlineRuntimeHost,
    ICloudSyncDataProvider,
    IRegionalDownloadPolicySink
{
    private const string SettingsPathOverrideEnvironmentVariable = "PCLN_LAUNCHER_SETTINGS_PATH";
    private readonly object _settingsLock = new();
    private readonly string _settingsPath;

    public DesktopOnlineRuntimeHost()
    {
        _settingsPath = CreateSettingsPath();
        SharedDataDirectory = Path.GetDirectoryName(_settingsPath) ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(SharedDataDirectory);
    }

    public string SharedDataDirectory { get; }

    public ICloudSyncDataProvider CloudSync => this;

    public IRegionalDownloadPolicySink RegionalDownloadPolicy => this;

    public bool IsEnabled => GetBoolean("Online.CloudSyncEnabled");

    public bool HasAnySectionEnabled =>
        GetBoolean("Online.CloudSyncAccount") ||
        GetBoolean("Online.CloudSyncFavorites") ||
        GetBoolean("Online.CloudSyncUiPreferences") ||
        GetBoolean("Online.CloudSyncHintPreferences") ||
        GetBoolean("Online.CloudSyncDownloadPreferences") ||
        GetBoolean("Online.CloudSyncLaunchPreferences") ||
        GetBoolean("Online.CloudSyncHomepagePreferences") ||
        GetBoolean("Online.CloudSyncMusicPreferences") ||
        GetBoolean("Online.CloudSyncUpdatePreferences") ||
        GetBoolean("Online.CloudSyncCustomVariables");

    public static void Configure()
    {
        OnlineRuntime.Configure(new DesktopOnlineRuntimeHost());
        OnlineUiScheduler.Configure(action => Dispatcher.UIThread.InvokeAsync(action).GetTask());
        WindowsOnlineAccountBridge.RegisterIfAvailable();
    }

    public string? GetSecret(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Environment.GetEnvironmentVariable($"PCL_{key}") ??
               Environment.GetEnvironmentVariable(key);
    }

    public string Text(string key, params object?[] args)
    {
        string text = key switch
        {
            "Common.State.Unknown" => "未知",
            "Online.Login.Title" => "Microsoft 登录",
            "Online.Login.Cancelled" => "登录已取消。",
            "Online.Login.ClientIdMissing" => "缺少 Microsoft 登录配置，无法开始登录。",
            "Online.Login.WindowsUnsupported" => "当前系统不支持 Windows 账户登录。",
            "Online.Login.XboxFailed" => "无法登录 Xbox Live，请稍后重试。",
            "Online.Login.XstsFailed" => "无法完成 Xbox 安全验证，请稍后重试。",
            "Online.Login.XstsCredentialMissing" => "Xbox 登录凭据不完整，请重新登录。",
            "Online.Login.MinecraftAuthFailed" => "无法登录 Minecraft 服务，请稍后重试。",
            "Online.Login.MinecraftProfileFailed" => "无法读取 Minecraft 档案，请稍后重试。",
            "Online.Login.MicrosoftAccount" => "Microsoft 账户",
            "Online.Login.SuccessOwned" => "已登录 {0}。",
            "Online.Login.SuccessProfileMissing" => "已登录 {0}，但这个账户还没有创建 Minecraft 档案。",
            "Online.Login.SuccessNotOwned" => "已登录 {0}，但这个账户没有正版 Minecraft。已为你创建离线档案。",
            "Online.Login.Required" => "请先登录 PCL N 在线账户。",
            _ => key
        };
        return args.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, args);
    }

    public string GetString(string key)
    {
        LauncherSettings settings = LoadSettings();
        return settings.TextOptions.TryGetValue(key, out string? value) ? value : string.Empty;
    }

    public void SetString(string key, string value)
    {
        UpdateSettings(settings =>
        {
            settings.TextOptions[key] = value;
            return settings;
        });
    }

    public bool GetBoolean(string key)
    {
        LauncherSettings settings = LoadSettings();
        string shortKey = TrimOnlinePrefix(key);
        if (settings.BooleanOptions.TryGetValue(key, out bool value))
            return value;
        if (!string.Equals(shortKey, key, StringComparison.Ordinal) &&
            settings.BooleanOptions.TryGetValue(shortKey, out value))
        {
            return value;
        }

        return false;
    }

    public void SetBoolean(string key, bool value)
    {
        UpdateSettings(settings =>
        {
            settings.BooleanOptions[key] = value;
            string shortKey = TrimOnlinePrefix(key);
            if (!string.Equals(shortKey, key, StringComparison.Ordinal))
                settings.BooleanOptions[shortKey] = value;
            return settings;
        });
    }

    public void Flush()
    {
    }

    public Dictionary<string, JsonObject> BuildSnapshot()
    {
        LauncherSettings settings = LoadSettings();
        Dictionary<string, JsonObject> snapshot = new(StringComparer.Ordinal);
        AddSection(snapshot, "account", "Online.CloudSyncAccount", () => new JsonObject
        {
            ["msId"] = GetString("Online.MsId"),
            ["userName"] = GetString("Online.MsUserName"),
            ["minecraftProfileName"] = GetString("Online.MsMinecraftProfileName"),
            ["uuid"] = GetString("Online.MsUuid"),
            ["avatarUrl"] = GetString("Online.MsAvatarUrl"),
            ["ownsMinecraft"] = GetBoolean("Online.MsOwnsMinecraft")
        });
        AddSection(snapshot, "downloadPreferences", "Online.CloudSyncDownloadPreferences", () => new JsonObject
        {
            ["downloadSource"] = settings.DownloadSource.ToString()
        });
        AddSection(snapshot, "uiPreferences", "Online.CloudSyncUiPreferences", () => new JsonObject
        {
            ["colorMode"] = settings.ColorMode.ToString(),
            ["lightColor"] = settings.LightColor.ToString(),
            ["darkColor"] = settings.DarkColor.ToString()
        });
        return snapshot;
    }

    public Task ApplySectionsAsync(IReadOnlyDictionary<string, JsonObject?> sections, bool overwriteAccount)
    {
        if (overwriteAccount && sections.TryGetValue("account", out JsonObject? account) && account is not null)
        {
            SetString("Online.MsId", account["msId"]?.ToString() ?? string.Empty);
            SetString("Online.MsUserName", account["userName"]?.ToString() ?? string.Empty);
            SetString("Online.MsMinecraftProfileName", account["minecraftProfileName"]?.ToString() ?? string.Empty);
            SetString("Online.MsUuid", account["uuid"]?.ToString() ?? string.Empty);
            SetString("Online.MsAvatarUrl", account["avatarUrl"]?.ToString() ?? string.Empty);
            if (account["ownsMinecraft"] is JsonValue ownsMinecraft &&
                ownsMinecraft.TryGetValue(out bool owns))
            {
                SetBoolean("Online.MsOwnsMinecraft", owns);
            }
        }

        return Task.CompletedTask;
    }

    public bool Apply(ClientRegionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        bool changed = false;
        UpdateSettings(settings =>
        {
            settings.TextOptions["Online.RegionCountryCode"] = policy.CountryCode;
            settings.TextOptions["Online.RegionDecisionSource"] = policy.DecisionSource;
            settings.TextOptions["Online.RegionRegulatoryNotice"] = policy.RegulatoryNotice;
            settings.BooleanOptions["Online.RegionIsChinaMainland"] = policy.IsChinaMainland;
            settings.BooleanOptions["Online.RegionAllowDomesticMirrorSwitch"] = policy.AllowDomesticMirrorSwitch;
            settings.BooleanOptions["Online.RegionUseDomesticMirror"] = policy.UseDomesticMirror;

            DownloadSourcePreference nextSource = settings.DownloadSource;
            if (!policy.AllowDomesticMirrorSwitch && !policy.UseDomesticMirror)
                nextSource = DownloadSourcePreference.OfficialOnly;
            else if (!policy.AllowDomesticMirrorSwitch && policy.UseDomesticMirror)
                nextSource = DownloadSourcePreference.MirrorOnly;

            if (nextSource != settings.DownloadSource)
            {
                changed = true;
                settings = settings with { DownloadSource = nextSource };
            }

            return settings;
        });
        return changed;
    }

    private static void AddSection(
        Dictionary<string, JsonObject> snapshot,
        string section,
        string enabledKey,
        Func<JsonObject> build)
    {
        if (OnlineRuntime.Host.GetBoolean(enabledKey))
            snapshot[section] = build();
    }

    private LauncherSettings LoadSettings()
    {
        lock (_settingsLock)
        {
            using LauncherSettingsStore store = new(_settingsPath);
            LauncherSettings settings = store.LoadAsync().AsTask().GetAwaiter().GetResult().Settings;
            return settings with
            {
                BooleanOptions = settings.BooleanOptions is null ? [] : new Dictionary<string, bool>(settings.BooleanOptions),
                IntegerOptions = settings.IntegerOptions is null ? [] : new Dictionary<string, int>(settings.IntegerOptions),
                TextOptions = settings.TextOptions is null ? [] : new Dictionary<string, string>(settings.TextOptions)
            };
        }
    }

    private void SaveSettings(LauncherSettings settings)
    {
        lock (_settingsLock)
        {
            using LauncherSettingsStore store = new(_settingsPath);
            store.SaveAsync(settings).AsTask().GetAwaiter().GetResult();
        }
    }

    private void UpdateSettings(Func<LauncherSettings, LauncherSettings> update)
    {
        LauncherSettings settings = LoadSettings();
        SaveSettings(update(settings));
    }

    private static string CreateSettingsPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable(SettingsPathOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath);

        DefaultPlatformPathProvider paths = new();
        return Path.Combine(paths.ApplicationDataDirectory, "PCL-N", "launcher-settings.json");
    }

    private static string TrimOnlinePrefix(string key) =>
        key.StartsWith("Online.", StringComparison.Ordinal) ? key["Online.".Length..] : key;
}
