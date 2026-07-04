// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using PCL.Core.IO.Net;

namespace PCL.Application.Launching;

public sealed class AuthlibInjectorService
{
    private static readonly string[] DefaultMetadataUrls =
    [
        "https://authlib-injector.yushi.moe/artifact/latest.json",
        "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json"
    ];

    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<string> _metadataUrls;

    public AuthlibInjectorService(HttpClient? httpClient = null, IReadOnlyList<string>? metadataUrls = null)
    {
        _httpClient = httpClient ?? PortableHttp.Client;
        _metadataUrls = metadataUrls ?? DefaultMetadataUrls;
    }

    public async Task<string> EnsureAsync(string targetPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        string fullTargetPath = Path.GetFullPath(targetPath);
        AuthlibArtifact artifact;
        try
        {
            artifact = await GetLatestArtifactAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (File.Exists(fullTargetPath) && ex is HttpRequestException or JsonException or IOException)
        {
            return fullTargetPath;
        }

        if (File.Exists(fullTargetPath) && await IsSha256MatchAsync(fullTargetPath, artifact.Sha256, cancellationToken).ConfigureAwait(false))
            return fullTargetPath;

        byte[] content = await DownloadArtifactAsync(artifact, cancellationToken).ConfigureAwait(false);
        if (!IsSha256Match(content, artifact.Sha256))
            throw new InvalidDataException("Authlib-Injector 下载文件校验失败。");

        Directory.CreateDirectory(Path.GetDirectoryName(fullTargetPath)
                                  ?? throw new InvalidOperationException("Authlib-Injector 缓存路径没有父目录。"));
        string temporaryPath = fullTargetPath + "." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, content, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, fullTargetPath, overwrite: true);
        return fullTargetPath;
    }

    public async Task<string> GetServerMetadataAsync(string authServer, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authServer);
        using HttpRequestMessage request = new(HttpMethod.Get, NormalizeAuthServer(authServer));
        ConfigureRequest(request);
        using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await PortableHttp.ReadStringAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public static string NormalizeAuthServer(string authServer)
    {
        string normalized = authServer.Trim().TrimEnd('/');
        const string authServerSuffix = "/authserver";
        if (normalized.EndsWith(authServerSuffix, StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^authServerSuffix.Length];
        return normalized;
    }

    private async Task<AuthlibArtifact> GetLatestArtifactAsync(CancellationToken cancellationToken)
    {
        List<Exception> errors = [];
        foreach (string metadataUrl in _metadataUrls)
        {
            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, metadataUrl);
                ConfigureRequest(request);
                using HttpResponseMessage response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string json = await PortableHttp.ReadStringAsync(response, cancellationToken).ConfigureAwait(false);
                return ParseArtifact(json);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException)
            {
                errors.Add(ex);
            }
        }

        throw new HttpRequestException("无法获取 Authlib-Injector 下载信息。", new AggregateException(errors));
    }

    private async Task<byte[]> DownloadArtifactAsync(AuthlibArtifact artifact, CancellationToken cancellationToken)
    {
        List<Exception> errors = [];
        foreach (string url in GetDownloadUrls(artifact.DownloadUrl))
        {
            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, url);
                ConfigureRequest(request);
                using HttpResponseMessage response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                errors.Add(ex);
            }
        }

        throw new HttpRequestException("无法下载 Authlib-Injector。", new AggregateException(errors));
    }

    private static AuthlibArtifact ParseArtifact(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string downloadUrl = root.GetProperty("download_url").GetString()
                             ?? throw new JsonException("Authlib-Injector 元数据缺少 download_url。");
        string sha256 = root.GetProperty("checksums").GetProperty("sha256").GetString()
                        ?? throw new JsonException("Authlib-Injector 元数据缺少 checksums.sha256。");
        return new AuthlibArtifact(downloadUrl, sha256);
    }

    private static string[] GetDownloadUrls(string downloadUrl)
    {
        string official = downloadUrl.Replace(
            "bmclapi2.bangbang93.com/mirrors/authlib-injector",
            "authlib-injector.yushi.moe",
            StringComparison.OrdinalIgnoreCase);
        string mirror = official.Replace(
            "authlib-injector.yushi.moe",
            "bmclapi2.bangbang93.com/mirrors/authlib-injector",
            StringComparison.OrdinalIgnoreCase);
        return string.Equals(official, mirror, StringComparison.OrdinalIgnoreCase)
            ? [official]
            : [official, mirror];
    }

    private static async Task<bool> IsSha256MatchAsync(string path, string expected, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return string.Equals(Convert.ToHexString(hash), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSha256Match(byte[] content, string expected)
    {
        byte[] hash = SHA256.HashData(content);
        return string.Equals(Convert.ToHexString(hash), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static void ConfigureRequest(HttpRequestMessage request)
    {
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PCL-N", "1.0"));
        string language = CultureInfo.CurrentUICulture.Name;
        request.Headers.AcceptLanguage.ParseAdd(string.IsNullOrWhiteSpace(language) ? "zh-CN" : language);
    }

    private sealed record AuthlibArtifact(string DownloadUrl, string Sha256);
}
