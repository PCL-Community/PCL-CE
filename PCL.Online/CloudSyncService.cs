// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;

namespace PCL.Online;

public static class CloudSyncService
{
    public enum SyncMode
    {
        TimestampMerge,
        RemoteOverwrite,
        LocalOverwrite
    }

    public enum NoticeType
    {
        Starting,
        Retry,
        Success,
        Failed
    }

    private const int MaxRetryCount = 3;
    private static readonly string MetadataFilePath = Path.Combine(Paths.SharedData, "online.sync.v1.json");
    private static int _syncing;
    private static int _isAvailable = 1;
    private static string _lastReason = "manual-retry";
    private static SyncMode _lastMode = SyncMode.TimestampMerge;

    public static event Action<NoticeType, int>? Notice;

    public static bool IsAvailable => Volatile.Read(ref _isAvailable) != 0;

    public static bool TrySyncInBackground(string reason, SyncMode mode = SyncMode.TimestampMerge)
    {
        if (!OnlineAccountService.IsLoggedIn ||
            !States.Online.CloudSyncEnabled ||
            !HasAnySectionEnabled())
            return false;

        if (Interlocked.CompareExchange(ref _syncing, 1, 0) != 0)
            return false;

        _lastReason = reason;
        _lastMode = mode;
        _ = Task.Run(async () =>
        {
            Notice?.Invoke(NoticeType.Starting, 0);
            try
            {
                for (var retry = 0; ; retry++)
                {
                    try
                    {
                        await SyncAsync(reason, mode).ConfigureAwait(false);
                        Interlocked.Exchange(ref _isAvailable, 1);
                        Notice?.Invoke(NoticeType.Success, 0);
                        return;
                    }
                    catch (Exception ex) when (retry < MaxRetryCount)
                    {
                        var retryNumber = retry + 1;
                        LogWrapper.Debug(ex, "CloudSync",
                            $"云同步失败（{reason}），准备第 {retryNumber}/{MaxRetryCount} 次重试。");
                        Notice?.Invoke(NoticeType.Retry, retryNumber);
                        await Task.Delay(TimeSpan.FromSeconds(retryNumber)).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, "CloudSync", $"云同步失败（{reason}）");
                Interlocked.Exchange(ref _isAvailable, 0);
                Notice?.Invoke(NoticeType.Failed, 0);
            }
            finally
            {
                Interlocked.Exchange(ref _syncing, 0);
            }
        });
        return true;
    }

    public static bool RetryLastFailed()
    {
        return TrySyncInBackground(_lastReason, _lastMode);
    }

    public static async Task DeleteCloudProfileAsync(CancellationToken cancellationToken = default)
    {
        if (!OnlineAccountService.EnsureAccountIdentity())
            throw new InvalidOperationException(Lang.Text("Online.Login.Required"));

        var msId = States.Online.MsId;
        if (string.IsNullOrWhiteSpace(msId))
            throw new InvalidOperationException("当前账户缺少 msid。");

        var serverBaseUrl = ResolveServerBaseUrl();
        if (string.IsNullOrWhiteSpace(serverBaseUrl))
            throw new InvalidOperationException("未配置在线服务地址。");

        using var cloudClient = NCloudHttpClient.Create(serverBaseUrl);
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{serverBaseUrl}/api/users/{Uri.EscapeDataString(msId)}");
        using var response = await cloudClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();

        TryDeleteLocalMetadata();
    }

    private static async Task SyncAsync(string reason, SyncMode mode,
        CancellationToken cancellationToken = default)
    {
        if (!OnlineAccountService.IsLoggedIn)
            return;
        if (!States.Online.CloudSyncEnabled)
            return;
        if (!HasAnySectionEnabled())
            return;

        if (!OnlineAccountService.EnsureAccountIdentity())
        {
            LogWrapper.Info("CloudSync", $"跳过云同步（{reason}）：当前账户缺少 msid。");
            return;
        }

        var msId = States.Online.MsId;
        if (string.IsNullOrWhiteSpace(msId))
            return;

        var serverBaseUrl = ResolveServerBaseUrl();
        if (string.IsNullOrWhiteSpace(serverBaseUrl))
        {
            LogWrapper.Info("CloudSync", $"跳过云同步（{reason}）：未配置在线服务地址。");
            return;
        }
        if (IsLocalDebugServerUrl(serverBaseUrl) &&
            !await IsServerReachableAsync(serverBaseUrl, cancellationToken).ConfigureAwait(false))
        {
            throw new HttpRequestException($"本地调试服务 {serverBaseUrl} 未启动。");
        }

        using var cloudClient = NCloudHttpClient.Create(serverBaseUrl);

        if (mode == SyncMode.LocalOverwrite)
        {
            var localSnapshot = BuildSnapshot();
            var localRequest = BuildRequest(localSnapshot,
                new CloudSyncMetadataFile { MsId = msId }, forceAllSections: true);
            if (!localRequest.HasAnySection)
                return;

            var localResult = await PostSyncAsync(serverBaseUrl, msId, localRequest.Request, cloudClient,
                    cancellationToken)
                .ConfigureAwait(false);
            await ApplyDocumentAsync(localResult).ConfigureAwait(false);
            SaveMetadata(CreateMetadataFromLocal(msId, localResult, BuildSnapshot()));
            LogWrapper.Info("CloudSync", $"云同步完成（{reason}，本地覆盖）。");
            return;
        }

        var metadata = LoadMetadata();
        if (!string.Equals(metadata.MsId, msId, StringComparison.Ordinal))
            metadata = new CloudSyncMetadataFile { MsId = msId };
        var isFirstSyncForAccount = metadata.Sections.Count == 0;

        var remoteDocument = await TryGetRemoteDocumentAsync(serverBaseUrl, msId, cloudClient, cancellationToken)
            .ConfigureAwait(false);

        if (mode == SyncMode.RemoteOverwrite)
        {
            if (remoteDocument is null)
            {
                LogWrapper.Info("CloudSync", $"云同步完成（{reason}）：云端暂无数据。");
                return;
            }

            await ApplyDocumentAsync(remoteDocument, overwriteAccount: true).ConfigureAwait(false);
            SaveMetadata(CreateMetadataFromLocal(msId, remoteDocument, BuildSnapshot()));
            LogWrapper.Info("CloudSync", $"云同步完成（{reason}，云端覆盖）。");
            return;
        }

        if (remoteDocument is not null)
            MergeMissingMetadata(metadata, remoteDocument);

        if (remoteDocument is not null && isFirstSyncForAccount)
        {
            await ApplyDocumentAsync(remoteDocument).ConfigureAwait(false);
            var localAfterPull = BuildSnapshot();
            var remoteMetadata = CreateMetadataFromRemote(msId, remoteDocument);
            var followUpRequest = BuildRequest(localAfterPull, remoteMetadata, forceAllSections: false);
            if (followUpRequest.HasAnySection)
            {
                var merged = await PostSyncAsync(serverBaseUrl, msId, followUpRequest.Request, cloudClient, cancellationToken)
                    .ConfigureAwait(false);
                await ApplyDocumentAsync(merged).ConfigureAwait(false);
                SaveMetadata(CreateMetadataFromLocal(msId, merged, BuildSnapshot()));
                LogWrapper.Info("CloudSync", $"云同步完成（{reason}，首次拉取后回传本地账户信息）。");
                return;
            }

            SaveMetadata(CreateMetadataFromLocal(msId, remoteDocument, localAfterPull));
            LogWrapper.Info("CloudSync", $"云同步完成（{reason}，首次拉取）。");
            return;
        }

        var snapshot = BuildSnapshot();
        var request = BuildRequest(snapshot, metadata, forceAllSections: remoteDocument is null && metadata.Sections.Count == 0);
        if (!request.HasAnySection)
        {
            if (remoteDocument is not null)
                SaveMetadata(CreateMetadataFromLocal(msId, remoteDocument, snapshot));
            return;
        }

        var result = await PostSyncAsync(serverBaseUrl, msId, request.Request, cloudClient, cancellationToken)
            .ConfigureAwait(false);
        await ApplyDocumentAsync(result).ConfigureAwait(false);
        SaveMetadata(CreateMetadataFromLocal(msId, result, BuildSnapshot()));
        LogWrapper.Info("CloudSync", $"云同步完成（{reason}）。");
    }

    internal static string ResolveServerBaseUrl()
    {
        var url = EnvironmentInterop.GetSecret("ONLINE_SERVER_URL");
        if (!string.IsNullOrWhiteSpace(url))
            return url.Trim().TrimEnd('/');
#if DEBUG
        return "http://127.0.0.1:5210";
#else
        return "https://115.29.230.105";
#endif
    }

    private static bool IsLocalDebugServerUrl(string serverBaseUrl)
    {
#if DEBUG
        return Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out var uri) && uri.IsLoopback;
#else
        return false;
#endif
    }

    private static async Task<bool> IsServerReachableAsync(string serverBaseUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out var uri))
            return false;

        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(500));
            await client.ConnectAsync(uri.Host, uri.Port, timeoutCts.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static CloudSyncMetadataFile LoadMetadata()
    {
        try
        {
            if (!File.Exists(MetadataFilePath))
                return new CloudSyncMetadataFile();

            var json = File.ReadAllText(MetadataFilePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<CloudSyncMetadataFile>(json, JsonCompat.SerializerOptions)
                   ?? new CloudSyncMetadataFile();
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "CloudSync", "读取本地同步元数据失败，将使用空状态继续。");
            return new CloudSyncMetadataFile();
        }
    }

    private static void SaveMetadata(CloudSyncMetadataFile metadata)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MetadataFilePath)!);
            var options = new JsonSerializerOptions(JsonCompat.SerializerOptions)
            {
                WriteIndented = true
            };
            File.WriteAllText(MetadataFilePath, JsonSerializer.Serialize(metadata, options), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "CloudSync", "写入本地同步元数据失败。");
        }
    }

    private static void TryDeleteLocalMetadata()
    {
        try
        {
            if (File.Exists(MetadataFilePath))
                File.Delete(MetadataFilePath);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "CloudSync", "删除本地同步元数据失败。");
        }
    }

    private static async Task<CloudUserDocument?> TryGetRemoteDocumentAsync(string serverBaseUrl, string msId,
        HttpClient cloudClient, CancellationToken cancellationToken)
    {
        using var response = await HttpRequest
            .Create($"{serverBaseUrl}/api/users/{Uri.EscapeDataString(msId)}")
            .SendAsync(httpClient: cloudClient, retryTimes: 0, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await response.EnsureSuccessStatusCodeWithContentAsync(cancellationToken).ConfigureAwait(false);
        return await response.AsJsonAsync<CloudUserDocument>(cancellationToken: cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new InvalidDataException("云端同步数据为空。");
    }

    private static async Task<CloudUserDocument> PostSyncAsync(string serverBaseUrl, string msId,
        CloudUserSyncRequest request, HttpClient cloudClient, CancellationToken cancellationToken)
    {
        using var response = await HttpRequest
            .CreatePost($"{serverBaseUrl}/api/users/{Uri.EscapeDataString(msId)}/sync")
            .WithJsonContent(request)
            .SendAsync(httpClient: cloudClient, retryTimes: 0, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await response.EnsureSuccessStatusCodeWithContentAsync(cancellationToken).ConfigureAwait(false);
        return (await response.AsJsonAsync<CloudUserDocument>(cancellationToken: cancellationToken).ConfigureAwait(false))!;
    }

    private static Dictionary<string, JsonObject> BuildSnapshot()
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
            ["custom_variables"] = JsonSerializer.SerializeToNode(States.CustomVariables ?? new Dictionary<string, string>(),
                JsonCompat.SerializerOptions) ?? new JsonObject()
        };
    }

    private static RequestBuildResult BuildRequest(Dictionary<string, JsonObject> snapshot,
        CloudSyncMetadataFile metadata, bool forceAllSections)
    {
        var now = DateTimeOffset.UtcNow;
        var request = new CloudUserSyncRequest();
        var hasAnySection = false;

        foreach (var pair in snapshot)
        {
            var key = pair.Key;
            var data = pair.Value;
            var hash = ComputeHash(data);
            metadata.Sections.TryGetValue(key, out var sectionMetadata);

            if (!forceAllSections && sectionMetadata is null && IsSectionMeaningfullyEmpty(key, data))
                continue;

            var updatedAt = forceAllSections || sectionMetadata is null || !string.Equals(sectionMetadata.Hash, hash, StringComparison.Ordinal)
                ? now
                : sectionMetadata.UpdatedAt;

            var section = new CloudSyncSection
            {
                Data = data.DeepClone(),
                UpdatedAt = updatedAt
            };
            SetSection(request, key, section);
            hasAnySection = true;
        }

        return new RequestBuildResult(request, hasAnySection);
    }

    private static bool IsSectionMeaningfullyEmpty(string key, JsonObject data)
    {
        return key switch
        {
            "account" => string.IsNullOrWhiteSpace(States.Online.MsId),
            "favorites" => data["comp_favorites"] is JsonArray { Count: 0 },
            "customVariables" => data["custom_variables"] is JsonObject { Count: 0 },
            _ => false
        };
    }

    private static void ApplyDocument(CloudUserDocument document, bool overwriteAccount = false)
    {
        if (States.Online.CloudSyncAccount)
            ApplyAccount(document.Account?.Data as JsonObject, overwriteAccount);
        if (States.Online.CloudSyncFavorites) ApplyFavorites(document.Favorites?.Data as JsonObject);
        if (States.Online.CloudSyncUiPreferences) ApplyUiPreferences(document.UiPreferences?.Data as JsonObject);
        if (States.Online.CloudSyncHintPreferences) ApplyHintPreferences(document.HintPreferences?.Data as JsonObject);
        if (States.Online.CloudSyncDownloadPreferences) ApplyDownloadPreferences(document.DownloadPreferences?.Data as JsonObject);
        if (States.Online.CloudSyncLaunchPreferences) ApplyLaunchPreferences(document.LaunchPreferences?.Data as JsonObject);
        if (States.Online.CloudSyncHomepagePreferences) ApplyHomepagePreferences(document.HomepagePreferences?.Data as JsonObject);
        if (States.Online.CloudSyncMusicPreferences) ApplyMusicPreferences(document.MusicPreferences?.Data as JsonObject);
        if (States.Online.CloudSyncUpdatePreferences) ApplyUpdatePreferences(document.UpdatePreferences?.Data as JsonObject);
        if (States.Online.CloudSyncCustomVariables) ApplyCustomVariables(document.CustomVariables?.Data as JsonObject);
    }

    private static Task ApplyDocumentAsync(CloudUserDocument document, bool overwriteAccount = false)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyDocument(document, overwriteAccount);
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(() => ApplyDocument(document, overwriteAccount), DispatcherPriority.Send).Task;
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
            TryGetString(data, "ms_avatar_url", out var avatarPath) &&
            File.Exists(avatarPath))
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
        SetEnum(data, "system_update_channel", value => Config.Update.UpdateChannel = (UpdateChannel)value);
    }

    private static void ApplyCustomVariables(JsonObject? data)
    {
        if (data?["custom_variables"] is null)
            return;

        var variables = data["custom_variables"]!.Deserialize<Dictionary<string, string>>(JsonCompat.SerializerOptions);
        if (variables is not null)
            States.CustomVariables = variables;
    }

    private static void MergeMissingMetadata(CloudSyncMetadataFile metadata, CloudUserDocument remoteDocument)
    {
        foreach (var (key, section) in EnumerateSections(remoteDocument))
        {
            if (section?.Data is null || metadata.Sections.ContainsKey(key))
                continue;

            metadata.Sections[key] = new CloudSyncSectionMetadata
            {
                Hash = ComputeHash(section.Data),
                UpdatedAt = section.UpdatedAt
            };
        }
    }

    private static CloudSyncMetadataFile CreateMetadataFromRemote(string msId, CloudUserDocument document)
    {
        var metadata = new CloudSyncMetadataFile { MsId = msId };
        foreach (var (key, section) in EnumerateSections(document))
        {
            if (section?.Data is null)
                continue;

            metadata.Sections[key] = new CloudSyncSectionMetadata
            {
                Hash = ComputeHash(section.Data),
                UpdatedAt = section.UpdatedAt
            };
        }
        return metadata;
    }

    private static CloudSyncMetadataFile CreateMetadataFromLocal(string msId, CloudUserDocument document,
        Dictionary<string, JsonObject> snapshot)
    {
        var metadata = new CloudSyncMetadataFile { MsId = msId };
        foreach (var (key, section) in EnumerateSections(document))
        {
            if (!snapshot.TryGetValue(key, out var localData))
                continue;

            metadata.Sections[key] = new CloudSyncSectionMetadata
            {
                Hash = ComputeHash(localData),
                UpdatedAt = section?.UpdatedAt ?? DateTimeOffset.UtcNow
            };
        }
        return metadata;
    }

    private static IEnumerable<(string Key, CloudSyncSection? Section)> EnumerateSections(CloudUserDocument document)
    {
        yield return ("account", document.Account);
        yield return ("favorites", document.Favorites);
        yield return ("uiPreferences", document.UiPreferences);
        yield return ("hintPreferences", document.HintPreferences);
        yield return ("downloadPreferences", document.DownloadPreferences);
        yield return ("launchPreferences", document.LaunchPreferences);
        yield return ("homepagePreferences", document.HomepagePreferences);
        yield return ("musicPreferences", document.MusicPreferences);
        yield return ("updatePreferences", document.UpdatePreferences);
        yield return ("customVariables", document.CustomVariables);
    }

    private static void SetSection(CloudUserSyncRequest request, string key, CloudSyncSection section)
    {
        switch (key)
        {
            case "account":
                request.Account = section;
                break;
            case "favorites":
                request.Favorites = section;
                break;
            case "uiPreferences":
                request.UiPreferences = section;
                break;
            case "hintPreferences":
                request.HintPreferences = section;
                break;
            case "downloadPreferences":
                request.DownloadPreferences = section;
                break;
            case "launchPreferences":
                request.LaunchPreferences = section;
                break;
            case "homepagePreferences":
                request.HomepagePreferences = section;
                break;
            case "musicPreferences":
                request.MusicPreferences = section;
                break;
            case "updatePreferences":
                request.UpdatePreferences = section;
                break;
            case "customVariables":
                request.CustomVariables = section;
                break;
        }
    }

    private static bool HasAnySectionEnabled()
    {
        return States.Online.CloudSyncAccount ||
               States.Online.CloudSyncFavorites ||
               States.Online.CloudSyncUiPreferences ||
               States.Online.CloudSyncHintPreferences ||
               States.Online.CloudSyncDownloadPreferences ||
               States.Online.CloudSyncLaunchPreferences ||
               States.Online.CloudSyncHomepagePreferences ||
               States.Online.CloudSyncMusicPreferences ||
               States.Online.CloudSyncUpdatePreferences ||
               States.Online.CloudSyncCustomVariables;
    }

    private static void AddSection(Dictionary<string, JsonObject> snapshot, string key, bool enabled,
        Func<JsonObject> factory)
    {
        if (enabled)
            snapshot[key] = factory();
    }

    private static string ComputeHash(JsonNode? node)
    {
        var text = node?.ToJsonString(JsonCompat.SerializerOptions) ?? "null";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash);
    }

    private static JsonNode ParseJsonOrDefault(string? raw, JsonNode fallback)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback.DeepClone();
            return JsonCompat.ParseNode(raw);
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

    private sealed class RequestBuildResult
    {
        public RequestBuildResult(CloudUserSyncRequest request, bool hasAnySection)
        {
            Request = request;
            HasAnySection = hasAnySection;
        }

        public CloudUserSyncRequest Request { get; }
        public bool HasAnySection { get; }
    }

    private sealed class CloudSyncMetadataFile
    {
        public string MsId { get; set; } = "";
        public Dictionary<string, CloudSyncSectionMetadata> Sections { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class CloudSyncSectionMetadata
    {
        public string Hash { get; set; } = "";
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private sealed class CloudSyncSection
    {
        public JsonNode? Data { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private sealed class CloudUserSyncRequest
    {
        public CloudSyncSection? Account { get; set; }
        public CloudSyncSection? Favorites { get; set; }
        public CloudSyncSection? UiPreferences { get; set; }
        public CloudSyncSection? HintPreferences { get; set; }
        public CloudSyncSection? DownloadPreferences { get; set; }
        public CloudSyncSection? LaunchPreferences { get; set; }
        public CloudSyncSection? HomepagePreferences { get; set; }
        public CloudSyncSection? MusicPreferences { get; set; }
        public CloudSyncSection? UpdatePreferences { get; set; }
        public CloudSyncSection? CustomVariables { get; set; }
    }

    private sealed class CloudUserDocument
    {
        public string MsId { get; set; } = "";
        public CloudSyncSection? Account { get; set; }
        public CloudSyncSection? Favorites { get; set; }
        public CloudSyncSection? UiPreferences { get; set; }
        public CloudSyncSection? HintPreferences { get; set; }
        public CloudSyncSection? DownloadPreferences { get; set; }
        public CloudSyncSection? LaunchPreferences { get; set; }
        public CloudSyncSection? HomepagePreferences { get; set; }
        public CloudSyncSection? MusicPreferences { get; set; }
        public CloudSyncSection? UpdatePreferences { get; set; }
        public CloudSyncSection? CustomVariables { get; set; }
    }
}
