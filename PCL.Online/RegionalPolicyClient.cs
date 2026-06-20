// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.IO.Net;
using PCL.Core.Serialization;

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
                PortableLog.Debug(ex, "RegionalPolicy", "刷新区域策略失败");
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
        using var response = await client.GetAsync($"{serverBaseUrl}/api/client/policy", cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var body = await PortableHttp.ReadStringAsync(response, cancellationToken).ConfigureAwait(false);
        var policy = OnlineJson.Deserialize<ClientRegionPolicy>(body)
                     ?? DefaultPolicy;

        _current = policy;
        ApplyDownloadPolicy(policy);
        return policy;
    }

    public static void ApplyDownloadPolicy(ClientRegionPolicy policy)
    {
        if (OnlineRuntime.Host.RegionalDownloadPolicy.Apply(policy))
            OnlineRuntime.Host.Flush();
    }
}
