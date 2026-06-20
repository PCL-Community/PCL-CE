// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
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
using PCL.Core.IO.Net;
using PCL.Core.Logging;
using PCL.Core.Serialization;

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
    private static int _syncing;
    private static int _isAvailable = 1;
    private static string _lastReason = "manual-retry";
    private static SyncMode _lastMode = SyncMode.TimestampMerge;

    public static event Action<NoticeType, int>? Notice;

    public static bool IsAvailable => Volatile.Read(ref _isAvailable) != 0;

    private static string MetadataFilePath =>
        Path.Combine(OnlineRuntime.Host.SharedDataDirectory, "online.sync.v1.json");

    public static bool TrySyncInBackground(string reason, SyncMode mode = SyncMode.TimestampMerge)
    {
        var cloudSync = OnlineRuntime.Host.CloudSync;
        if (!OnlineAccountService.IsLoggedIn ||
            !cloudSync.IsEnabled ||
            !cloudSync.HasAnySectionEnabled)
        {
            return false;
        }

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
                        PortableLog.Debug(ex, "CloudSync",
                            $"云同步失败（{reason}），准备第 {retryNumber}/{MaxRetryCount} 次重试。");
                        Notice?.Invoke(NoticeType.Retry, retryNumber);
                        await Task.Delay(TimeSpan.FromSeconds(retryNumber)).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                PortableLog.Debug(ex, "CloudSync", $"云同步失败（{reason}）");
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
            throw new InvalidOperationException(OnlineRuntime.Host.Text("Online.Login.Required"));

        var msId = OnlineAccountService.MsId;
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

    private static async Task SyncAsync(
        string reason,
        SyncMode mode,
        CancellationToken cancellationToken = default)
    {
        if (!OnlineAccountService.IsLoggedIn)
            return;

        var cloudSync = OnlineRuntime.Host.CloudSync;
        if (!cloudSync.IsEnabled || !cloudSync.HasAnySectionEnabled)
            return;

        if (!OnlineAccountService.EnsureAccountIdentity())
        {
            PortableLog.Info("CloudSync", $"跳过云同步（{reason}）：当前账户缺少 msid。");
            return;
        }

        var msId = OnlineAccountService.MsId;
        if (string.IsNullOrWhiteSpace(msId))
            return;

        var serverBaseUrl = ResolveServerBaseUrl();
        if (string.IsNullOrWhiteSpace(serverBaseUrl))
        {
            PortableLog.Info("CloudSync", $"跳过云同步（{reason}）：未配置在线服务地址。");
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
            PortableLog.Info("CloudSync", $"云同步完成（{reason}，本地覆盖）。");
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
                PortableLog.Info("CloudSync", $"云同步完成（{reason}）：云端暂无数据。");
                return;
            }

            await ApplyDocumentAsync(remoteDocument, overwriteAccount: true).ConfigureAwait(false);
            SaveMetadata(CreateMetadataFromLocal(msId, remoteDocument, BuildSnapshot()));
            PortableLog.Info("CloudSync", $"云同步完成（{reason}，云端覆盖）。");
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
                var merged = await PostSyncAsync(serverBaseUrl, msId, followUpRequest.Request, cloudClient,
                        cancellationToken)
                    .ConfigureAwait(false);
                await ApplyDocumentAsync(merged).ConfigureAwait(false);
                SaveMetadata(CreateMetadataFromLocal(msId, merged, BuildSnapshot()));
                PortableLog.Info("CloudSync", $"云同步完成（{reason}，首次拉取后回传本地账户信息）。");
                return;
            }

            SaveMetadata(CreateMetadataFromLocal(msId, remoteDocument, localAfterPull));
            PortableLog.Info("CloudSync", $"云同步完成（{reason}，首次拉取）。");
            return;
        }

        var snapshot = BuildSnapshot();
        var request = BuildRequest(snapshot, metadata,
            forceAllSections: remoteDocument is null && metadata.Sections.Count == 0);
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
        PortableLog.Info("CloudSync", $"云同步完成（{reason}）。");
    }

    internal static string ResolveServerBaseUrl()
    {
        var url = OnlineRuntime.Host.GetSecret("ONLINE_SERVER_URL");
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
            return OnlineJson.Deserialize<CloudSyncMetadataFile>(json)
                   ?? new CloudSyncMetadataFile();
        }
        catch (Exception ex)
        {
            PortableLog.Debug(ex, "CloudSync", "读取本地同步元数据失败，将使用空状态继续。");
            return new CloudSyncMetadataFile();
        }
    }

    private static void SaveMetadata(CloudSyncMetadataFile metadata)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MetadataFilePath)!);
            File.WriteAllText(MetadataFilePath, OnlineJson.Serialize(metadata, writeIndented: true), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            PortableLog.Debug(ex, "CloudSync", "写入本地同步元数据失败。");
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
            PortableLog.Debug(ex, "CloudSync", "删除本地同步元数据失败。");
        }
    }

    private static async Task<CloudUserDocument?> TryGetRemoteDocumentAsync(
        string serverBaseUrl,
        string msId,
        HttpClient cloudClient,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{serverBaseUrl}/api/users/{Uri.EscapeDataString(msId)}");
        using var response = await cloudClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        var body = await EnsureSuccessAndReadAsync(response, cancellationToken).ConfigureAwait(false);
        return OnlineJson.Deserialize<CloudUserDocument>(body)
               ?? throw new InvalidDataException("云端同步数据为空。");
    }

    private static async Task<CloudUserDocument> PostSyncAsync(
        string serverBaseUrl,
        string msId,
        CloudUserSyncRequest syncRequest,
        HttpClient cloudClient,
        CancellationToken cancellationToken)
    {
        var body = OnlineJson.Serialize(syncRequest);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{serverBaseUrl}/api/users/{Uri.EscapeDataString(msId)}/sync")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        using var response = await cloudClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var responseBody = await EnsureSuccessAndReadAsync(response, cancellationToken).ConfigureAwait(false);
        return OnlineJson.Deserialize<CloudUserDocument>(responseBody)
               ?? throw new InvalidDataException("云端同步数据为空。");
    }

    private static async Task<string> EnsureSuccessAndReadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await PortableHttp.ReadStringAsync(response, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
            return body;

        var preview = TrimForLog(body);
        throw new HttpRequestException(
            $"云同步服务返回 HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {preview}");
    }

    private static Dictionary<string, JsonObject> BuildSnapshot()
    {
        return OnlineRuntime.Host.CloudSync.BuildSnapshot();
    }

    private static RequestBuildResult BuildRequest(
        Dictionary<string, JsonObject> snapshot,
        CloudSyncMetadataFile metadata,
        bool forceAllSections)
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

            var updatedAt = forceAllSections ||
                            sectionMetadata is null ||
                            !string.Equals(sectionMetadata.Hash, hash, StringComparison.Ordinal)
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
            "favorites" => data["comp_favorites"] is JsonArray { Count: 0 },
            "customVariables" => data["custom_variables"] is JsonObject { Count: 0 },
            _ => IsJsonObjectEmpty(data)
        };
    }

    private static async Task ApplyDocumentAsync(CloudUserDocument document, bool overwriteAccount = false)
    {
        var sections = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
        foreach (var (key, section) in EnumerateSections(document))
            sections[key] = section?.Data as JsonObject;

        await OnlineUiScheduler.InvokeAsync(() =>
        {
            OnlineRuntime.Host.CloudSync.ApplySectionsAsync(sections, overwriteAccount)
                .GetAwaiter()
                .GetResult();
        }).ConfigureAwait(false);
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

    private static CloudSyncMetadataFile CreateMetadataFromLocal(
        string msId,
        CloudUserDocument document,
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

    private static bool IsJsonObjectEmpty(JsonObject data)
    {
        foreach (var pair in data)
        {
            if (!IsJsonValueEmpty(pair.Value))
                return false;
        }

        return true;
    }

    private static bool IsJsonValueEmpty(JsonNode? node)
    {
        if (node is null)
            return true;

        if (node is JsonArray array)
            return array.Count == 0;

        if (node is JsonObject obj)
            return obj.Count == 0 || IsJsonObjectEmpty(obj);

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return string.IsNullOrWhiteSpace(text);
            if (value.TryGetValue<bool>(out var boolValue))
                return !boolValue;
        }

        return false;
    }

    private static string ComputeHash(JsonNode? node)
    {
        var text = node?.ToJsonString(PortableJson.SerializerOptions) ?? "null";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash);
    }

    private static string TrimForLog(string value)
    {
        value = value.ReplaceLineEndings(" ").Trim();
        return value.Length <= 300 ? value : value[..300] + "...";
    }

    private sealed class RequestBuildResult(CloudUserSyncRequest request, bool hasAnySection)
    {
        public CloudUserSyncRequest Request { get; } = request;

        public bool HasAnySection { get; } = hasAnySection;
    }

}
