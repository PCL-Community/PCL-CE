// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PCL.Online;

public static class NCloudHttpClient
{
    public const string DefaultServerBaseUrl = "https://115.29.230.105/";
    private const string CertificateResourceName = "PCL.Online.Certificates.n-cloud.cer";
    private const string ClientCertificateResourceName = "PCL.Online.Certificates.n-cloud-client.pfx";

    public static HttpClient Create(string serverBaseUrl)
    {
        if (!Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out var serverUri))
            throw new InvalidOperationException("N Cloud 服务器地址无效。");

        if (serverUri.Scheme == Uri.UriSchemeHttp)
        {
            if (!serverUri.IsLoopback)
                throw new InvalidOperationException("N Cloud 非本地服务器必须使用 HTTPS。");

            return CreateClient(null);
        }

        if (serverUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("N Cloud 服务器地址仅支持 HTTP 或 HTTPS。");

        var pinnedHash = LoadPinnedCertificateHash();
        if (pinnedHash is null)
            throw new InvalidOperationException(
                "未配置 N Cloud 固定证书。请嵌入 Certificates/n-cloud.cer，或在 Debug 环境设置 PCL_ONLINE_SERVER_CERT_SHA256。");

        return CreateClient((_, certificate, _, errors) =>
            ValidatePinnedCertificate(certificate, errors, pinnedHash));
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>? validationCallback)
    {
        var handler = new HttpClientHandler
        {
            UseProxy = true,
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 20,
            UseCookies = false
        };

        if (validationCallback is not null)
            handler.ServerCertificateCustomValidationCallback = validationCallback;

        var clientCertificate = LoadClientCertificate();
        if (clientCertificate is not null)
            handler.ClientCertificates.Add(clientCertificate);

        return new HttpClient(handler, disposeHandler: true);
    }

    private static byte[]? LoadPinnedCertificateHash()
    {
        var assembly = typeof(NCloudHttpClient).Assembly;
        using var certificateStream = assembly.GetManifestResourceStream(CertificateResourceName);
        if (certificateStream is not null)
        {
            using var memoryStream = new MemoryStream();
            certificateStream.CopyTo(memoryStream);
            using var certificate = LoadCertificate(memoryStream.ToArray());
            return SHA256.HashData(certificate.RawData);
        }

#if DEBUG
        var configuredHash = OnlineRuntime.Host.GetSecret("ONLINE_SERVER_CERT_SHA256");
        return ParseSha256(configuredHash);
#else
        return null;
#endif
    }

    private static byte[]? ParseSha256(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = new string(value.Where(Uri.IsHexDigit).ToArray());
        return normalized.Length == 64 ? Convert.FromHexString(normalized) : null;
    }

    private static X509Certificate2 LoadCertificate(byte[] certificateData)
    {
        var text = Encoding.ASCII.GetString(certificateData);
        return text.Contains("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal)
            ? X509Certificate2.CreateFromPem(text)
            : X509CertificateLoader.LoadCertificate(certificateData);
    }

    private static X509Certificate2? LoadClientCertificate()
    {
        var password = OnlineRuntime.Host.GetSecret("ONLINE_CLIENT_CERT_PASSWORD");
        var configuredPath = OnlineRuntime.Host.GetSecret("ONLINE_CLIENT_CERT_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            return X509CertificateLoader.LoadPkcs12FromFile(configuredPath, password,
                GetClientCertificateStorageFlags());

        var assembly = typeof(NCloudHttpClient).Assembly;
        using var certificateStream = assembly.GetManifestResourceStream(ClientCertificateResourceName);
        if (certificateStream is null)
            return null;

        using var memoryStream = new MemoryStream();
        certificateStream.CopyTo(memoryStream);
        return X509CertificateLoader.LoadPkcs12(memoryStream.ToArray(), password,
            GetClientCertificateStorageFlags());
    }

    private static X509KeyStorageFlags GetClientCertificateStorageFlags() =>
        OperatingSystem.IsWindows()
            ? X509KeyStorageFlags.UserKeySet
            : X509KeyStorageFlags.EphemeralKeySet;

    private static bool ValidatePinnedCertificate(
        X509Certificate2? certificate,
        SslPolicyErrors errors,
        byte[] pinnedHash)
    {
        if (certificate is null ||
            errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable) ||
            errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            return false;

        var now = DateTime.UtcNow;
        if (now < certificate.NotBefore.ToUniversalTime() ||
            now > certificate.NotAfter.ToUniversalTime())
            return false;

        var actualHash = SHA256.HashData(certificate.RawData);
        return CryptographicOperations.FixedTimeEquals(actualHash, pinnedHash);
    }
}
