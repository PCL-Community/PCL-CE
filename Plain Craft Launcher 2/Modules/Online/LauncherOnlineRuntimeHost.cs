using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.Online;

namespace PCL;

internal sealed class LauncherOnlineRuntimeHost :
    IOnlineRuntimeHost,
    ICloudSyncDataProvider,
    IRegionalDownloadPolicySink
{
    public string SharedDataDirectory => Paths.SharedData;

    public ICloudSyncDataProvider CloudSync => this;

    public IRegionalDownloadPolicySink RegionalDownloadPolicy => this;

    public bool IsEnabled => States.Online.CloudSyncEnabled;

    public bool HasAnySectionEnabled =>
        States.Online.CloudSyncAccount ||
        States.Online.CloudSyncFavorites ||
        States.Online.CloudSyncUiPreferences ||
        States.Online.CloudSyncHintPreferences ||
        States.Online.CloudSyncDownloadPreferences ||
        States.Online.CloudSyncLaunchPreferences ||
        States.Online.CloudSyncHomepagePreferences ||
        States.Online.CloudSyncMusicPreferences ||
        States.Online.CloudSyncUpdatePreferences ||
        States.Online.CloudSyncCustomVariables;

    public string? GetSecret(string key)
    {
        return key switch
        {
            "MS_CLIENT_ID" => Secrets.MSOAuthClientId,
            _ => EnvironmentInterop.GetSecret(key)
        };
    }

    public string Text(string key, params object?[] args)
    {
        return args.Length == 0 ? Lang.Text(key) : Lang.Text(key, args);
    }

    public string GetString(string key)
    {
        return key switch
        {
            "Online.LegalAcceptedVersion" => States.Online.LegalAcceptedVersion,
            "Online.MsId" => States.Online.MsId,
            "Online.MsAccessToken" => States.Online.MsAccessToken,
            "Online.MsOAuthRefreshToken" => States.Online.MsOAuthRefreshToken,
            "Online.MsGraphAccessToken" => States.Online.MsGraphAccessToken,
            "Online.MsGraphRefreshToken" => States.Online.MsGraphRefreshToken,
            "Online.MsUserName" => States.Online.MsUserName,
            "Online.MsMinecraftProfileName" => States.Online.MsMinecraftProfileName,
            "Online.MsUuid" => States.Online.MsUuid,
            "Online.MsAvatarUrl" => States.Online.MsAvatarUrl,
            "Online.MsLastTokenRefresh" => States.Online.MsLastTokenRefresh,
            "Online.MsMissingProfilePromptedKeys" => States.Online.MissingMinecraftProfilePromptedKeys,
            _ => ""
        };
    }

    public void SetString(string key, string value)
    {
        switch (key)
        {
            case "Online.LegalAcceptedVersion":
                States.Online.LegalAcceptedVersion = value;
                break;
            case "Online.MsId":
                States.Online.MsId = value;
                break;
            case "Online.MsAccessToken":
                States.Online.MsAccessToken = value;
                break;
            case "Online.MsOAuthRefreshToken":
                States.Online.MsOAuthRefreshToken = value;
                break;
            case "Online.MsGraphAccessToken":
                States.Online.MsGraphAccessToken = value;
                break;
            case "Online.MsGraphRefreshToken":
                States.Online.MsGraphRefreshToken = value;
                break;
            case "Online.MsUserName":
                States.Online.MsUserName = value;
                break;
            case "Online.MsMinecraftProfileName":
                States.Online.MsMinecraftProfileName = value;
                break;
            case "Online.MsUuid":
                States.Online.MsUuid = value;
                break;
            case "Online.MsAvatarUrl":
                States.Online.MsAvatarUrl = value;
                break;
            case "Online.MsLastTokenRefresh":
                States.Online.MsLastTokenRefresh = value;
                break;
            case "Online.MsMissingProfilePromptedKeys":
                States.Online.MissingMinecraftProfilePromptedKeys = value;
                break;
        }
    }

    public bool GetBoolean(string key)
    {
        return key switch
        {
            "Online.MsOwnsMinecraft" => States.Online.MsOwnsMinecraft,
            "Online.CloudSyncEnabled" => States.Online.CloudSyncEnabled,
            "Online.CloudSyncAccount" => States.Online.CloudSyncAccount,
            "Online.CloudSyncFavorites" => States.Online.CloudSyncFavorites,
            "Online.CloudSyncUiPreferences" => States.Online.CloudSyncUiPreferences,
            "Online.CloudSyncHintPreferences" => States.Online.CloudSyncHintPreferences,
            "Online.CloudSyncDownloadPreferences" => States.Online.CloudSyncDownloadPreferences,
            "Online.CloudSyncLaunchPreferences" => States.Online.CloudSyncLaunchPreferences,
            "Online.CloudSyncHomepagePreferences" => States.Online.CloudSyncHomepagePreferences,
            "Online.CloudSyncMusicPreferences" => States.Online.CloudSyncMusicPreferences,
            "Online.CloudSyncUpdatePreferences" => States.Online.CloudSyncUpdatePreferences,
            "Online.CloudSyncCustomVariables" => States.Online.CloudSyncCustomVariables,
            _ => false
        };
    }

    public void SetBoolean(string key, bool value)
    {
        switch (key)
        {
            case "Online.MsOwnsMinecraft":
                States.Online.MsOwnsMinecraft = value;
                break;
            case "Online.CloudSyncEnabled":
                States.Online.CloudSyncEnabled = value;
                break;
            case "Online.CloudSyncAccount":
                States.Online.CloudSyncAccount = value;
                break;
            case "Online.CloudSyncFavorites":
                States.Online.CloudSyncFavorites = value;
                break;
            case "Online.CloudSyncUiPreferences":
                States.Online.CloudSyncUiPreferences = value;
                break;
            case "Online.CloudSyncHintPreferences":
                States.Online.CloudSyncHintPreferences = value;
                break;
            case "Online.CloudSyncDownloadPreferences":
                States.Online.CloudSyncDownloadPreferences = value;
                break;
            case "Online.CloudSyncLaunchPreferences":
                States.Online.CloudSyncLaunchPreferences = value;
                break;
            case "Online.CloudSyncHomepagePreferences":
                States.Online.CloudSyncHomepagePreferences = value;
                break;
            case "Online.CloudSyncMusicPreferences":
                States.Online.CloudSyncMusicPreferences = value;
                break;
            case "Online.CloudSyncUpdatePreferences":
                States.Online.CloudSyncUpdatePreferences = value;
                break;
            case "Online.CloudSyncCustomVariables":
                States.Online.CloudSyncCustomVariables = value;
                break;
        }
    }

    public void Flush()
    {
        ConfigService.FlushAll();
    }

    public Dictionary<string, JsonObject> BuildSnapshot()
    {
        var snapshot = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        AddSection(snapshot, "account", States.Online.CloudSyncAccount, BuildAccountSection);
        AddSection(snapshot, "favorites", States.Online.CloudSyncFavorites, BuildFavoritesSection);
        AddSection(snapshot, "uiPreferences", States.Online.CloudSyncUiPreferences, BuildUiPreferencesSection);
        AddSection(snapshot, "hintPreferences", States.Online.CloudSyncHintPreferences, BuildHintPreferencesSection);
        AddSection(snapshot, "downloadPreferences", States.Online.CloudSyncDownloadPreferences, BuildDownloadPreferencesSection);
        AddSection(snapshot, "launchPreferences", States.Online.CloudSyncLaunchPreferences, BuildLaunchPreferencesSection);
        AddSection(snapshot, "homepagePreferences", States.Online.CloudSyncHomepagePreferences, BuildHomepagePreferencesSection);
        AddSection(snapshot, "musicPreferences", States.Online.CloudSyncMusicPreferences, BuildMusicPreferencesSection);
        AddSection(snapshot, "updatePreferences", States.Online.CloudSyncUpdatePreferences, BuildUpdatePreferencesSection);
        AddSection(snapshot, "customVariables", States.Online.CloudSyncCustomVariables, BuildCustomVariablesSection);
        return snapshot;
    }

    public Task ApplySectionsAsync(IReadOnlyDictionary<string, JsonObject?> sections, bool overwriteAccount)
    {
        if (States.Online.CloudSyncAccount)
            ApplyAccount(GetSection(sections, "account"), overwriteAccount);
        if (States.Online.CloudSyncFavorites)
            ApplyFavorites(GetSection(sections, "favorites"));
        if (States.Online.CloudSyncUiPreferences)
            ApplyUiPreferences(GetSection(sections, "uiPreferences"));
        if (States.Online.CloudSyncHintPreferences)
            ApplyHintPreferences(GetSection(sections, "hintPreferences"));
        if (States.Online.CloudSyncDownloadPreferences)
            ApplyDownloadPreferences(GetSection(sections, "downloadPreferences"));
        if (States.Online.CloudSyncLaunchPreferences)
            ApplyLaunchPreferences(GetSection(sections, "launchPreferences"));
        if (States.Online.CloudSyncHomepagePreferences)
            ApplyHomepagePreferences(GetSection(sections, "homepagePreferences"));
        if (States.Online.CloudSyncMusicPreferences)
            ApplyMusicPreferences(GetSection(sections, "musicPreferences"));
        if (States.Online.CloudSyncUpdatePreferences)
            ApplyUpdatePreferences(GetSection(sections, "updatePreferences"));
        if (States.Online.CloudSyncCustomVariables)
            ApplyCustomVariables(GetSection(sections, "customVariables"));

        Flush();
        return Task.CompletedTask;
    }

    public bool Apply(ClientRegionPolicy policy)
    {
        if (policy.AllowDomesticMirrorSwitch)
            return false;

        var targetSource = policy.UseDomesticMirror ? 0 : 2;
        var changed = false;
        if (Config.Download.FileSource != targetSource)
        {
            Config.Download.FileSource = targetSource;
            changed = true;
        }

        if (Config.Download.VersionListSource != targetSource)
        {
            Config.Download.VersionListSource = targetSource;
            changed = true;
        }

        return changed;
    }

    private static JsonObject BuildAccountSection()
    {
        return new JsonObject
        {
            ["msid"] = States.Online.MsId,
            ["ms_user_name"] = States.Online.MsUserName,
            ["ms_uuid"] = States.Online.MsUuid,
            ["ms_avatar_url"] = States.Online.MsAvatarUrl,
            ["ms_owns_minecraft"] = States.Online.MsOwnsMinecraft,
            ["minecraft_profile_name"] = States.Online.MsMinecraftProfileName,
            ["legal_accepted_version"] = States.Online.LegalAcceptedVersion
        };
    }

    private static JsonObject BuildFavoritesSection()
    {
        return new JsonObject
        {
            ["comp_favorites"] = ParseJsonOrDefault(States.Game.CompFavorites, new JsonArray())
        };
    }

    private static JsonObject BuildUiPreferencesSection()
    {
        var hide = Config.Preference.Hide;
        return new JsonObject
        {
            ["ui_language"] = Config.Preference.Localization.Language,
            ["ui_format_culture"] = Config.Preference.Localization.FormatCulture,
            ["ui_region"] = Config.Preference.Localization.Region,
            ["ui_dark_mode"] = (int)Config.Preference.Theme.ColorMode,
            ["ui_dark_color"] = (int)Config.Preference.Theme.DarkColor,
            ["ui_light_color"] = (int)Config.Preference.Theme.LightColor,
            ["ui_launcher_theme"] = Config.Preference.Theme.ThemeSelected,
            ["ui_launcher_hue"] = Config.Preference.Theme.WindowHue,
            ["ui_launcher_sat"] = Config.Preference.Theme.WindowSat,
            ["ui_launcher_light"] = Config.Preference.Theme.WindowLight,
            ["ui_launcher_delta"] = Config.Preference.Theme.WindowDelta,
            ["ui_launcher_logo"] = Config.Preference.ShowStartupLogo,
            ["ui_show_launching_hint"] = Config.Preference.ShowLaunchingHint,
            ["ui_hint_align_right"] = Config.Preference.HintAlignRight,
            ["ui_logo_type"] = (int)Config.Preference.WindowTitleType,
            ["ui_logo_text"] = Config.Preference.WindowTitleCustomText,
            ["ui_logo_left"] = Config.Preference.TopBarLeftAlign,
            ["ui_font"] = Config.Preference.Font,
            ["ui_motd_font"] = Config.Preference.MotdFont,
            ["detailed_instance_classification"] = Config.Preference.DetailedInstanceClassification,
            ["ui_background_colorful"] = Config.Preference.Background.BackgroundColorful,
            ["ui_background_opacity"] = Config.Preference.Background.WallpaperOpacity,
            ["ui_background_carousel"] = Config.Preference.Background.WallpaperCarousel,
            ["ui_background_blur"] = Config.Preference.Background.WallpaperBlurRadius,
            ["ui_background_suit"] = Config.Preference.Background.WallpaperSuitMode,
            ["ui_auto_pause_video"] = Config.Preference.Background.AutoPauseVideo,
            ["ui_blur"] = Config.Preference.Blur.IsEnabled,
            ["ui_blur_value"] = Config.Preference.Blur.Radius,
            ["ui_blur_sampling_rate"] = Config.Preference.Blur.SamplingRate,
            ["ui_blur_type"] = Config.Preference.Blur.KernelType,
            ["ui_hidden_pages"] = new JsonObject
            {
                ["page_download"] = hide.PageDownload,
                ["page_setup"] = hide.PageSetup,
                ["page_tools"] = hide.PageTools
            },
            ["ui_hidden_tools"] = new JsonObject
            {
                ["tools_help"] = hide.ToolsHelp,
                ["tools_test"] = hide.ToolsTest
            },
            ["ui_hidden_instance_tabs"] = new JsonObject
            {
                ["instance_edit"] = hide.InstanceEdit,
                ["instance_export"] = hide.InstanceExport,
                ["instance_save"] = hide.InstanceSave,
                ["instance_screenshot"] = hide.InstanceScreenshot,
                ["instance_mod"] = hide.InstanceMod,
                ["instance_resource_pack"] = hide.InstanceResourcePack,
                ["instance_shader"] = hide.InstanceShader,
                ["instance_schematic"] = hide.InstanceSchematic,
                ["instance_server"] = hide.InstanceServer
            },
            ["ui_hidden_functions"] = new JsonObject
            {
                ["function_select"] = hide.FunctionSelect,
                ["function_mod_update"] = hide.FunctionModUpdate,
                ["function_hidden"] = hide.FunctionHidden
            }
        };
    }

    private static JsonObject BuildHintPreferencesSection()
    {
        return new JsonObject
        {
            ["hint_download_thread"] = States.Hint.LargeDownloadThread,
            ["hint_renderer"] = States.Hint.Renderer,
            ["hint_debug_log4j2_config"] = States.Hint.DebugLog4j2Config,
            ["hint_install_back"] = States.Hint.InstallPageBack,
            ["hint_hide"] = States.Hint.HideGameInstance,
            ["hint_hand_install"] = States.Hint.ManualInstall,
            ["hint_clear_rubbish"] = States.Hint.CleanJunkFile,
            ["hint_update_mod"] = States.Hint.UpdateMod,
            ["hint_custom_command"] = States.Hint.HomepageCommand,
            ["hint_custom_warn"] = States.Hint.UntrustedHomepage,
            ["hint_more_advanced_setup"] = States.Hint.MoreInstanceSetup,
            ["hint_indie_setup"] = States.Hint.IndieSetup,
            ["hint_profile_select"] = States.Hint.LaunchWithProfile,
            ["hint_export_config"] = States.Hint.ExportConfig,
            ["hint_max_log"] = States.Hint.MaxGameLog,
            ["hint_non_ascii_game_path"] = States.Hint.NonAsciiGamePath,
            ["ui_launcher_ce_hint"] = States.Hint.CEMessage,
            ["ui_schematic_first_time"] = States.Hint.SchematicFirstTime,
            ["showed_announcements"] = States.Hint.ShowedAnnouncements,
            ["hint_datapack_update"] = States.Hint.FunctionDatapackUpdate
        };
    }

    private static JsonObject BuildDownloadPreferencesSection()
    {
        return new JsonObject
        {
            ["download_thread_limit"] = Config.Download.ThreadLimit,
            ["download_speed_limit"] = Config.Download.SpeedLimit,
            ["download_file_source"] = Config.Download.FileSource,
            ["download_version_source"] = Config.Download.VersionListSource,
            ["download_auto_select_instance"] = Config.Download.AutoSelectInstance,
            ["download_fix_authlib"] = Config.Download.FixAuthLib,
            ["comp_name_format_v1"] = Config.Download.Comp.NameFormatV1,
            ["comp_name_format_v2"] = Config.Download.Comp.NameFormatV2,
            ["comp_ignore_quilt"] = Config.Download.Comp.IgnoreQuilt,
            ["comp_auto_install_dependencies"] = Config.Download.Comp.AutoInstallDependencies,
            ["comp_read_clipboard"] = Config.Download.Comp.ReadClipboard,
            ["comp_source_solution"] = Config.Download.Comp.CompSourceSolution,
            ["comp_local_name_style"] = Config.Download.Comp.UiCompNameSolution
        };
    }

    private static JsonObject BuildLaunchPreferencesSection()
    {
        return new JsonObject
        {
            ["launch_preferred_ip_stack"] = (int)Config.Launch.PreferredIpStack,
            ["launch_disable_jlw"] = Config.Launch.DisableJlw,
            ["launch_disable_rw"] = Config.Launch.DisableRw,
            ["launch_set_gpu_preference"] = Config.Launch.SetGpuPreference,
            ["launch_no_javaw"] = Config.Launch.NoJavaw,
            ["launch_disable_lwjgl_unsafe_agent"] = Config.Launch.DisableLwjglUnsafeAgent,
            ["launch_title"] = Config.Launch.Title,
            ["launch_type_info"] = Config.Launch.TypeInfo,
            ["launch_indie_solution_v1"] = Config.Launch.IndieSolutionV1,
            ["launch_indie_solution_v2"] = Config.Launch.IndieSolutionV2,
            ["launch_launcher_visibility"] = (int)Config.Launch.LauncherVisibility,
            ["launch_process_priority"] = (int)Config.Launch.ProcessPriority,
            ["launch_login_ms_auth_type"] = Config.Launch.LoginMsAuthType
        };
    }

    private static JsonObject BuildHomepagePreferencesSection()
    {
        return new JsonObject
        {
            ["ui_custom_type"] = Config.Preference.Homepage.Type,
            ["ui_custom_preset"] = Config.Preference.Homepage.SelectedPreset,
            ["ui_custom_net"] = Config.Preference.Homepage.CustomUrl,
            ["cache_saved_page_url"] = States.UI.SavedHomepageUrl,
            ["cache_saved_page_version"] = States.UI.SavedHomepageVersion
        };
    }

    private static JsonObject BuildMusicPreferencesSection()
    {
        return new JsonObject
        {
            ["ui_music_volume"] = Config.Preference.Music.Volume,
            ["ui_music_stop"] = Config.Preference.Music.StopInGame,
            ["ui_music_start"] = Config.Preference.Music.StartInGame,
            ["ui_music_auto"] = Config.Preference.Music.StartOnStartup,
            ["ui_music_random"] = Config.Preference.Music.ShufflePlayback,
            ["ui_music_smtc"] = Config.Preference.Music.EnableSMTC
        };
    }

    private static JsonObject BuildUpdatePreferencesSection()
    {
        return new JsonObject
        {
            ["tool_help_chinese"] = Config.Tool.AutoChangeLanguage,
            ["tool_update_release"] = Config.Tool.ReleaseNotification,
            ["tool_update_snapshot"] = Config.Tool.SnapshotNotification,
            ["system_system_update"] = (int)Config.Update.UpdateMode,
            ["system_update_channel"] = (int)Config.Update.UpdateChannel
        };
    }

    private static JsonObject BuildCustomVariablesSection()
    {
        return new JsonObject
        {
            ["custom_variables"] = JsonSerializer.SerializeToNode(
                States.CustomVariables ?? new Dictionary<string, string>(),
                JsonCompat.SerializerOptions) ?? new JsonObject()
        };
    }

    private static void ApplyAccount(JsonObject? data, bool overwrite)
    {
        if (data is null)
            return;

        if (TryGetString(data, "legal_accepted_version", out var acceptedVersion) &&
            !string.IsNullOrWhiteSpace(acceptedVersion))
            States.Online.LegalAcceptedVersion = acceptedVersion;
        if ((overwrite || string.IsNullOrWhiteSpace(States.Online.MsId)) &&
            TryGetString(data, "msid", out var msId))
            States.Online.MsId = msId;
        if ((overwrite || string.IsNullOrWhiteSpace(States.Online.MsUserName)) &&
            TryGetString(data, "ms_user_name", out var msUserName))
            States.Online.MsUserName = msUserName;
        if ((overwrite || string.IsNullOrWhiteSpace(States.Online.MsMinecraftProfileName)) &&
            TryGetString(data, "minecraft_profile_name", out var mcName))
            States.Online.MsMinecraftProfileName = mcName;
        if ((overwrite || string.IsNullOrWhiteSpace(States.Online.MsUuid)) &&
            TryGetString(data, "ms_uuid", out var uuid))
            States.Online.MsUuid = uuid;
        if ((overwrite || !States.Online.MsOwnsMinecraft) &&
            TryGetBool(data, "ms_owns_minecraft", out var ownsMinecraft))
            States.Online.MsOwnsMinecraft = ownsMinecraft;
        if (string.IsNullOrWhiteSpace(States.Online.MsAvatarUrl) &&
            TryGetString(data, "ms_avatar_url", out var avatarPath))
            States.Online.MsAvatarUrl = avatarPath;
    }

    private static void ApplyFavorites(JsonObject? data)
    {
        if (data?["comp_favorites"] is null)
            return;

        States.Game.CompFavorites = data["comp_favorites"]!.ToJsonString(JsonCompat.SerializerOptions);
    }

    private static void ApplyUiPreferences(JsonObject? data)
    {
        if (data is null)
            return;

        SetString(data, "ui_language", value => Config.Preference.Localization.Language = value);
        SetString(data, "ui_format_culture", value => Config.Preference.Localization.FormatCulture = value);
        SetString(data, "ui_region", value => Config.Preference.Localization.Region = value);
        SetEnum(data, "ui_dark_mode", value => Config.Preference.Theme.ColorMode = (ColorMode)value);
        SetEnum(data, "ui_dark_color", value => Config.Preference.Theme.DarkColor = (ColorTheme)value);
        SetEnum(data, "ui_light_color", value => Config.Preference.Theme.LightColor = (ColorTheme)value);
        SetInt(data, "ui_launcher_theme", value => Config.Preference.Theme.ThemeSelected = value);
        SetInt(data, "ui_launcher_hue", value => Config.Preference.Theme.WindowHue = value);
        SetInt(data, "ui_launcher_sat", value => Config.Preference.Theme.WindowSat = value);
        SetInt(data, "ui_launcher_light", value => Config.Preference.Theme.WindowLight = value);
        SetInt(data, "ui_launcher_delta", value => Config.Preference.Theme.WindowDelta = value);
        SetBool(data, "ui_launcher_logo", value => Config.Preference.ShowStartupLogo = value);
        SetBool(data, "ui_show_launching_hint", value => Config.Preference.ShowLaunchingHint = value);
        SetBool(data, "ui_hint_align_right", value => Config.Preference.HintAlignRight = value);
        SetEnum(data, "ui_logo_type", value => Config.Preference.WindowTitleType = (LauncherTitleType)value);
        SetString(data, "ui_logo_text", value => Config.Preference.WindowTitleCustomText = value);
        SetBool(data, "ui_logo_left", value => Config.Preference.TopBarLeftAlign = value);
        SetString(data, "ui_font", value => Config.Preference.Font = value);
        SetString(data, "ui_motd_font", value => Config.Preference.MotdFont = value);
        SetBool(data, "detailed_instance_classification", value => Config.Preference.DetailedInstanceClassification = value);
        SetBool(data, "ui_background_colorful", value => Config.Preference.Background.BackgroundColorful = value);
        SetInt(data, "ui_background_opacity", value => Config.Preference.Background.WallpaperOpacity = value);
        SetInt(data, "ui_background_carousel", value => Config.Preference.Background.WallpaperCarousel = value);
        SetInt(data, "ui_background_blur", value => Config.Preference.Background.WallpaperBlurRadius = value);
        SetInt(data, "ui_background_suit", value => Config.Preference.Background.WallpaperSuitMode = value);
        SetBool(data, "ui_auto_pause_video", value => Config.Preference.Background.AutoPauseVideo = value);
        SetBool(data, "ui_blur", value => Config.Preference.Blur.IsEnabled = value);
        SetInt(data, "ui_blur_value", value => Config.Preference.Blur.Radius = value);
        SetInt(data, "ui_blur_sampling_rate", value => Config.Preference.Blur.SamplingRate = value);
        SetInt(data, "ui_blur_type", value => Config.Preference.Blur.KernelType = value);

        if (data["ui_hidden_pages"] is JsonObject hiddenPages)
        {
            SetBool(hiddenPages, "page_download", value => Config.Preference.Hide.PageDownload = value);
            SetBool(hiddenPages, "page_setup", value => Config.Preference.Hide.PageSetup = value);
            SetBool(hiddenPages, "page_tools", value => Config.Preference.Hide.PageTools = value);
        }

        if (data["ui_hidden_tools"] is JsonObject hiddenTools)
        {
            SetBool(hiddenTools, "tools_help", value => Config.Preference.Hide.ToolsHelp = value);
            SetBool(hiddenTools, "tools_test", value => Config.Preference.Hide.ToolsTest = value);
        }

        if (data["ui_hidden_instance_tabs"] is JsonObject hiddenTabs)
        {
            SetBool(hiddenTabs, "instance_edit", value => Config.Preference.Hide.InstanceEdit = value);
            SetBool(hiddenTabs, "instance_export", value => Config.Preference.Hide.InstanceExport = value);
            SetBool(hiddenTabs, "instance_save", value => Config.Preference.Hide.InstanceSave = value);
            SetBool(hiddenTabs, "instance_screenshot", value => Config.Preference.Hide.InstanceScreenshot = value);
            SetBool(hiddenTabs, "instance_mod", value => Config.Preference.Hide.InstanceMod = value);
            SetBool(hiddenTabs, "instance_resource_pack", value => Config.Preference.Hide.InstanceResourcePack = value);
            SetBool(hiddenTabs, "instance_shader", value => Config.Preference.Hide.InstanceShader = value);
            SetBool(hiddenTabs, "instance_schematic", value => Config.Preference.Hide.InstanceSchematic = value);
            SetBool(hiddenTabs, "instance_server", value => Config.Preference.Hide.InstanceServer = value);
        }

        if (data["ui_hidden_functions"] is JsonObject hiddenFunctions)
        {
            SetBool(hiddenFunctions, "function_select", value => Config.Preference.Hide.FunctionSelect = value);
            SetBool(hiddenFunctions, "function_mod_update", value => Config.Preference.Hide.FunctionModUpdate = value);
            SetBool(hiddenFunctions, "function_hidden", value => Config.Preference.Hide.FunctionHidden = value);
        }
    }

    private static void ApplyHintPreferences(JsonObject? data)
    {
        if (data is null)
            return;

        SetBool(data, "hint_download_thread", value => States.Hint.LargeDownloadThread = value);
        SetBool(data, "hint_renderer", value => States.Hint.Renderer = value);
        SetBool(data, "hint_debug_log4j2_config", value => States.Hint.DebugLog4j2Config = value);
        SetBool(data, "hint_install_back", value => States.Hint.InstallPageBack = value);
        SetBool(data, "hint_hide", value => States.Hint.HideGameInstance = value);
        SetBool(data, "hint_hand_install", value => States.Hint.ManualInstall = value);
        SetInt(data, "hint_clear_rubbish", value => States.Hint.CleanJunkFile = value);
        SetBool(data, "hint_update_mod", value => States.Hint.UpdateMod = value);
        SetBool(data, "hint_custom_command", value => States.Hint.HomepageCommand = value);
        SetBool(data, "hint_custom_warn", value => States.Hint.UntrustedHomepage = value);
        SetBool(data, "hint_more_advanced_setup", value => States.Hint.MoreInstanceSetup = value);
        SetBool(data, "hint_indie_setup", value => States.Hint.IndieSetup = value);
        SetBool(data, "hint_profile_select", value => States.Hint.LaunchWithProfile = value);
        SetBool(data, "hint_export_config", value => States.Hint.ExportConfig = value);
        SetBool(data, "hint_max_log", value => States.Hint.MaxGameLog = value);
        SetBool(data, "hint_non_ascii_game_path", value => States.Hint.NonAsciiGamePath = value);
        SetBool(data, "ui_launcher_ce_hint", value => States.Hint.CEMessage = value);
        SetBool(data, "ui_schematic_first_time", value => States.Hint.SchematicFirstTime = value);
        SetString(data, "showed_announcements", value => States.Hint.ShowedAnnouncements = value);
        SetBool(data, "hint_datapack_update", value => States.Hint.FunctionDatapackUpdate = value);
    }

    private static void ApplyDownloadPreferences(JsonObject? data)
    {
        if (data is null)
            return;

        SetInt(data, "download_thread_limit", value => Config.Download.ThreadLimit = value);
        SetInt(data, "download_speed_limit", value => Config.Download.SpeedLimit = value);
        SetInt(data, "download_file_source", value => Config.Download.FileSource = value);
        SetInt(data, "download_version_source", value => Config.Download.VersionListSource = value);
        SetBool(data, "download_auto_select_instance", value => Config.Download.AutoSelectInstance = value);
        SetBool(data, "download_fix_authlib", value => Config.Download.FixAuthLib = value);
        SetInt(data, "comp_name_format_v1", value => Config.Download.Comp.NameFormatV1 = value);
        SetInt(data, "comp_name_format_v2", value => Config.Download.Comp.NameFormatV2 = value);
        SetBool(data, "comp_ignore_quilt", value => Config.Download.Comp.IgnoreQuilt = value);
        SetBool(data, "comp_auto_install_dependencies", value => Config.Download.Comp.AutoInstallDependencies = value);
        SetBool(data, "comp_read_clipboard", value => Config.Download.Comp.ReadClipboard = value);
        SetInt(data, "comp_source_solution", value => Config.Download.Comp.CompSourceSolution = value);
        SetInt(data, "comp_local_name_style", value => Config.Download.Comp.UiCompNameSolution = value);
    }

    private static void ApplyLaunchPreferences(JsonObject? data)
    {
        if (data is null)
            return;

        SetEnum(data, "launch_preferred_ip_stack", value => Config.Launch.PreferredIpStack = (JvmPreferredIpStack)value);
        SetBool(data, "launch_disable_jlw", value => Config.Launch.DisableJlw = value);
        SetBool(data, "launch_disable_rw", value => Config.Launch.DisableRw = value);
        SetBool(data, "launch_set_gpu_preference", value => Config.Launch.SetGpuPreference = value);
        SetBool(data, "launch_no_javaw", value => Config.Launch.NoJavaw = value);
        SetBool(data, "launch_disable_lwjgl_unsafe_agent", value => Config.Launch.DisableLwjglUnsafeAgent = value);
        SetString(data, "launch_title", value => Config.Launch.Title = value);
        SetString(data, "launch_type_info", value => Config.Launch.TypeInfo = value);
        SetInt(data, "launch_indie_solution_v1", value => Config.Launch.IndieSolutionV1 = value);
        SetInt(data, "launch_indie_solution_v2", value => Config.Launch.IndieSolutionV2 = value);
        SetEnum(data, "launch_launcher_visibility", value => Config.Launch.LauncherVisibility = (LauncherVisibility)value);
        SetEnum(data, "launch_process_priority", value => Config.Launch.ProcessPriority = (GameProcessPriority)value);
        SetInt(data, "launch_login_ms_auth_type", value => Config.Launch.LoginMsAuthType = value);
    }

    private static void ApplyHomepagePreferences(JsonObject? data)
    {
        if (data is null)
            return;

        SetInt(data, "ui_custom_type", value => Config.Preference.Homepage.Type = value);
        SetInt(data, "ui_custom_preset", value => Config.Preference.Homepage.SelectedPreset = value);
        SetString(data, "ui_custom_net", value => Config.Preference.Homepage.CustomUrl = value);
        SetString(data, "cache_saved_page_url", value => States.UI.SavedHomepageUrl = value);
        SetString(data, "cache_saved_page_version", value => States.UI.SavedHomepageVersion = value);
    }

    private static void ApplyMusicPreferences(JsonObject? data)
    {
        if (data is null)
            return;

        SetInt(data, "ui_music_volume", value => Config.Preference.Music.Volume = value);
        SetBool(data, "ui_music_stop", value => Config.Preference.Music.StopInGame = value);
        SetBool(data, "ui_music_start", value => Config.Preference.Music.StartInGame = value);
        SetBool(data, "ui_music_auto", value => Config.Preference.Music.StartOnStartup = value);
        SetBool(data, "ui_music_random", value => Config.Preference.Music.ShufflePlayback = value);
        SetBool(data, "ui_music_smtc", value => Config.Preference.Music.EnableSMTC = value);
    }

    private static void ApplyUpdatePreferences(JsonObject? data)
    {
        if (data is null)
            return;

        SetBool(data, "tool_help_chinese", value => Config.Tool.AutoChangeLanguage = value);
        SetBool(data, "tool_update_release", value => Config.Tool.ReleaseNotification = value);
        SetBool(data, "tool_update_snapshot", value => Config.Tool.SnapshotNotification = value);
        SetEnum(data, "system_system_update", value => Config.Update.UpdateMode = (LauncherAutoUpdateBehavior)value);
        SetEnum(data, "system_update_channel",
            value => Config.Update.UpdateChannel = (PCL.Core.App.UpdateChannel)value);
    }

    private static void ApplyCustomVariables(JsonObject? data)
    {
        if (data?["custom_variables"] is null)
            return;

        var variables = data["custom_variables"]!.Deserialize<Dictionary<string, string>>(JsonCompat.SerializerOptions);
        if (variables is not null)
            States.CustomVariables = variables;
    }

    private static JsonObject? GetSection(IReadOnlyDictionary<string, JsonObject?> sections, string key)
    {
        return sections.TryGetValue(key, out var section) ? section : null;
    }

    private static void AddSection(Dictionary<string, JsonObject> snapshot, string key, bool enabled,
        Func<JsonObject> factory)
    {
        if (enabled)
            snapshot[key] = factory();
    }

    private static JsonNode ParseJsonOrDefault(string? raw, JsonNode fallback)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback.DeepClone();
            return JsonCompat.ParseNode(raw) ?? fallback.DeepClone();
        }
        catch
        {
            return fallback.DeepClone();
        }
    }

    private static bool TryGetString(JsonObject source, string key, out string value)
    {
        value = "";
        if (source[key] is null)
            return false;

        if (source[key] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue))
        {
            value = stringValue ?? "";
            return true;
        }

        value = source[key]!.ToString();
        return true;
    }

    private static bool TryGetInt(JsonObject source, string key, out int value)
    {
        value = default;
        if (source[key] is null)
            return false;

        if (source[key] is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<int>(out value))
                return true;
            if (jsonValue.TryGetValue<string>(out var stringValue) && int.TryParse(stringValue, out value))
                return true;
        }

        return false;
    }

    private static bool TryGetBool(JsonObject source, string key, out bool value)
    {
        value = default;
        if (source[key] is null)
            return false;

        if (source[key] is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<bool>(out value))
                return true;
            if (jsonValue.TryGetValue<string>(out var stringValue))
            {
                if (bool.TryParse(stringValue, out value))
                    return true;
                if (int.TryParse(stringValue, out var number))
                {
                    value = number != 0;
                    return true;
                }
            }
            if (jsonValue.TryGetValue<int>(out var intValue))
            {
                value = intValue != 0;
                return true;
            }
        }

        return false;
    }

    private static void SetString(JsonObject source, string key, Action<string> setter)
    {
        if (TryGetString(source, key, out var value))
            setter(value);
    }

    private static void SetInt(JsonObject source, string key, Action<int> setter)
    {
        if (TryGetInt(source, key, out var value))
            setter(value);
    }

    private static void SetBool(JsonObject source, string key, Action<bool> setter)
    {
        if (TryGetBool(source, key, out var value))
            setter(value);
    }

    private static void SetEnum(JsonObject source, string key, Action<int> setter)
    {
        if (TryGetInt(source, key, out var value))
            setter(value);
    }
}
