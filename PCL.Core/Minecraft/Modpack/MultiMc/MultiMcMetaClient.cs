using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Modpack.MultiMc;

/// <summary>
/// 基于 HTTP 的组件元数据客户端。
/// <para>
/// 依次尝试 Prism 与 MultiMC 两个镜像：Prism 的元数据仓库是 MultiMC 的超集，
/// 且仍在持续更新，因此优先使用；MultiMC 作为回退保证老整合包仍可解析。
/// </para>
/// </summary>
public sealed class MultiMcMetaClient : IMultiMcMetaClient
{
    /// <summary>默认的元数据源，按优先级排列。</summary>
    public static IReadOnlyList<string> DefaultEndpoints { get; } =
    [
        "https://meta.prismlauncher.org/v1",
        "https://meta.multimc.org/v1"
    ];

    private readonly IReadOnlyList<string> _endpoints;

    /// <summary>共享实例，使用默认元数据源。</summary>
    public static MultiMcMetaClient Shared { get; } = new();

    public MultiMcMetaClient(IReadOnlyList<string>? endpoints = null)
        => _endpoints = endpoints is { Count: > 0 } ? endpoints : DefaultEndpoints;

    public async Task<MultiMcPatch?> TryGetPatchAsync(
        string uid, string version, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(version)) return null;

        foreach (var endpoint in _endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = $"{endpoint.TrimEnd('/')}/{Uri.EscapeDataString(uid)}/{Uri.EscapeDataString(version)}.json";
            try
            {
                using var response = await HttpRequest.Create(url)
                    .SendAsync(retryTimes: 1, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                var content = await response.AsStringAsync(cancellationToken).ConfigureAwait(false);
                var patch = MultiMcPatch.TryCreate(JsonCompat.ParseNode(content), MultiMcPatchSource.Remote, uid);

                if (patch is not null)
                {
                    LogWrapper.Info("Modpack", $"已获取 MultiMC 组件元数据：{uid} {version}（来自 {endpoint}）");
                    return patch;
                }

                LogWrapper.Debug("Modpack", $"MultiMC 组件元数据内容非法：{url}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 组件不存在是常态（例如整合包自定义的组件），不应中断安装
                LogWrapper.Debug("Modpack", $"获取 MultiMC 组件元数据失败（{url}）：{ex.Message}");
            }
        }

        return null;
    }
}
