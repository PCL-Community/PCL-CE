using PCL.Core.IO.Download.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace PCL.Core.IO.Download.Network;

/// <summary>
/// HTTP元数据获取器
/// </summary>
public class MetadataProber
{
    /// <summary>
    /// 获取元数据
    /// </summary>
    /// <param name="urls">镜像源（包括主链接）</param>
    /// <param name="client">HTTP客户端</param>
    /// <returns></returns>
    /// <exception cref="FailedOperationException">无法获取到元信息</exception>
    public async Task<(long FileSize, bool SupportRange, List<MirrorInfo> SortedMirrors)>
        ProbeAsync(List<string> urls, HttpClient client)
    {
        var probeTasks = urls.Select(url => _ProbeSingleUrlAsync(url, client)).ToList();
        var results = await Task.WhenAll(probeTasks).ConfigureAwait(false);

        var successfulResults = results.Where(r => r is { IsSueccess: true, FileSize: > 0 }).ToList();

        if (successfulResults.Count == 0)
        {
            throw new FailedOperationException("All mirrors cannot connect or wrong response");
        }

        var consensusGroup = successfulResults
            .GroupBy(r => $"{r.ETag}_{r.FileSize}")
            .OrderByDescending(g => g.Count())
            .First()
            .ToList();

        if (consensusGroup.Count < successfulResults.Count)
        {
            // TODO:Log warning about inconsistent metadata among mirrors or do nothing?
        }

        var finalFileSize = consensusGroup[0].FileSize;
        var finalSupprotRange = consensusGroup.Any(r => r.SupportRange);

        var sortedMirrors = consensusGroup
            .OrderBy(r => r.LatencyMs)
            .Select((r, index) => new MirrorInfo
            {
                Url = r.Url,
                IsAlive = true,
                LatencyMilliseconds = r.LatencyMs,
                HealthScore = Math.Max(100 - (index * 5), 50)
            })
            .ToList();

        return (finalFileSize, finalSupprotRange, sortedMirrors);
    }

    private record ProbeResult(
        string Url,
        bool IsSueccess,
        long LatencyMs,
        long FileSize,
        bool SupportRange,
        string ETag
    );

    private static readonly Dictionary<string, ProbeResult> _ProbeCache = [];

    private static async Task<ProbeResult> _ProbeSingleUrlAsync(string url, HttpClient client)
    {
        if (_ProbeCache.TryGetValue(url, out var cache))
        {
            return cache;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);

            if (response is
                {
                    IsSuccessStatusCode: false,
                    StatusCode: HttpStatusCode.MethodNotAllowed or HttpStatusCode.Forbidden
                })
            {
                request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new RangeHeaderValue(0, 0);

                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
            }

            response.EnsureSuccessStatusCode();
            sw.Stop();

            // get fileSize
            var fileSize = response.Content.Headers.ContentLength ?? 0L;

            if (response is
                {
                    StatusCode: HttpStatusCode.PartialContent,
                    Content.Headers.ContentRange: not null
                })
            {
                fileSize = response.Content.Headers.ContentRange.Length ?? fileSize;
            }

            // checkout is support range
            var supportRange = response.Headers.AcceptRanges.Contains("bytes") ||
                               response.StatusCode == HttpStatusCode.PartialContent;

            // set eTag
            var eTag = response.Headers.ETag?.Tag?.Trim('"') ?? "NO_ETAG";

            var result = new ProbeResult(url, true, sw.ElapsedMilliseconds, fileSize, supportRange, eTag);
            _ProbeCache.Add(url, result);
            return result;
        }
        catch
        {
            sw.Stop();
            var result = new ProbeResult(url, false, sw.ElapsedMilliseconds, 0, false, string.Empty);
            _ProbeCache.Add(url, result);
            return result;
        }
    }
}