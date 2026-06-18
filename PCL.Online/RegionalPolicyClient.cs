// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;

namespace PCL.Online;

public sealed class ClientRegionPolicy
{
    public string CountryCode { get; set; } = "UN";
    public string DecisionSource { get; set; } = "default";
    public bool IsChinaMainland { get; set; }
    public bool UseDomesticMirror { get; set; }
    public bool AllowDomesticMirrorSwitch { get; set; } = true;
    public string RegulatoryNotice { get; set; } = "";
    public string? ClientIp { get; set; }
}

public static class RegionalPolicyClient
{
    private static readonly ClientRegionPolicy DefaultPolicy = new();
    private static ClientRegionPolicy? _current;
    private static int _refreshing;

    public static ClientRegionPolicy Current => _current ?? DefaultPolicy;

    public static void RefreshInBackground()
    {
        if (Interlocked.CompareExchange(ref _refreshing, 1, 0) != 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, "RegionalPolicy", "刷新区域策略失败");
            }
            finally
            {
                Interlocked.Exchange(ref _refreshing, 0);
            }
        });
    }

    public static async Task<ClientRegionPolicy> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var serverBaseUrl = CloudSyncService.ResolveServerBaseUrl();
        using var client = NCloudHttpClient.Create(serverBaseUrl);
        using var response = await HttpRequest.Create($"{serverBaseUrl}/api/client/policy")
            .SendAsync(httpClient: client, retryTimes: 0, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await response.EnsureSuccessStatusCodeWithContentAsync(cancellationToken).ConfigureAwait(false);
        var policy = await response.AsJsonAsync<ClientRegionPolicy>(cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? DefaultPolicy;

        _current = policy;
        ApplyDownloadPolicy(policy);
        return policy;
    }

    public static void ApplyDownloadPolicy(ClientRegionPolicy policy)
    {
        var changed = false;
        if (policy.UseDomesticMirror)
        {
            if (Config.Download.FileSource == 1)
            {
                Config.Download.FileSource = 0;
                changed = true;
            }

            if (Config.Download.VersionListSource == 1)
            {
                Config.Download.VersionListSource = 0;
                changed = true;
            }
        }
        else if (!policy.AllowDomesticMirrorSwitch)
        {
            if (Config.Download.FileSource == 0)
            {
                Config.Download.FileSource = 2;
                changed = true;
            }

            if (Config.Download.VersionListSource == 0)
            {
                Config.Download.VersionListSource = 2;
                changed = true;
            }
        }

        if (changed)
            ConfigService.FlushAll();
    }
}
