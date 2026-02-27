using PCL.Core.IO.Download.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Download.Network;

/// <summary>
/// HTTP元数据探测器 - 负责评估镜像源的可用性和性能特征
/// </summary>
public class MetadataProber
{
    private const int BandwidthProbeBytes = 16384; // 16KB 用于带宽估算

    /// <summary>
    /// 探测所有镜像源，返回经过验证和排序的镜像列表
    /// </summary>
    /// <exception cref="FailedOperationException">所有镜像均不可用</exception>
    public async Task<(long FileSize, bool SupportRange, List<MirrorInfo> SortedMirrors)>
        ProbeAsync(List<string> urls, HttpClient client, CancellationToken ct = default)
    {
        var probeTasks = urls.Select(url => _ProbeSingleUrlAsync(url, client, ct)).ToList();
        var results = await Task.WhenAll(probeTasks).ConfigureAwait(false);

        var successfulResults = results.Where(r => r.IsSuccess && r.FileSize > 0).ToList();

        if (successfulResults.Count == 0)
            throw new FailedOperationException("All mirrors failed probe");

        // 共识机制: 按 ETag + FileSize 分组，选择最大共识组
        var consensusGroup = successfulResults
            .GroupBy(r => (r.ETag, r.FileSize))
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key.FileSize) // 相同数量时优先选大文件组
            .First()
            .ToList();

        var finalFileSize = consensusGroup[0].FileSize;
        var finalSupportRange = consensusGroup.Any(r => r.SupportRange);

        // 多因子综合评分排序
        var sortedMirrors = consensusGroup
            .Select(r => new
            {
                Result = r,
                Score = ComputeInitialScore(r, consensusGroup)
            })
            .OrderByDescending(x => x.Score)
            .Select((x, index) => new MirrorInfo
            {
                Url = x.Result.Url,
                IsAlive = true,
                LatencyMilliseconds = x.Result.LatencyMs,
                EstimatedBandwidthBps = x.Result.EstimatedBandwidthBps,
                HealthScore = ComputeHealthScore(index, consensusGroup.Count)
            })
            .ToList();

        return (finalFileSize, finalSupportRange, sortedMirrors);
    }

    /// <summary>
    /// 计算初始综合评分
    /// </summary>
    private static double ComputeInitialScore(ProbeResult result, List<ProbeResult> allResults)
    {
        // 延迟评分 (指数衰减, 100ms 以下高分)
        var latencyScore = Math.Exp(-result.LatencyMs / 150.0);

        // 带宽评分 (对数归一化)
        var maxBandwidth = allResults.Max(r => r.EstimatedBandwidthBps);
        var bandwidthScore = maxBandwidth > 0
            ? Math.Log(1 + result.EstimatedBandwidthBps) / Math.Log(1 + maxBandwidth)
            : 0.5;

        // TTFB 评分 (首字节时间)
        var ttfbScore = Math.Exp(-result.TimeToFirstByteMs / 200.0);

        // 权重组合
        return 0.35 * latencyScore + 0.40 * bandwidthScore + 0.25 * ttfbScore;
    }

    /// <summary>
    /// 基于排名计算初始健康分数
    /// </summary>
    private static int ComputeHealthScore(int rank, int total)
    {
        // 非线性衰减: 前几名差距小，后面差距大
        var normalizedRank = (double)rank / Math.Max(total - 1, 1);
        var score = 100 - (int)(50 * Math.Pow(normalizedRank, 0.7));
        return Math.Max(score, 30);
    }

    private record ProbeResult(
        string Url,
        bool IsSuccess,
        long LatencyMs,
        long TimeToFirstByteMs,
        long FileSize,
        bool SupportRange,
        string ETag,
        double EstimatedBandwidthBps
    );

    private static readonly ConcurrentDictionary<string, ProbeResult> _ProbeCache = new();

    private static async Task<ProbeResult> _ProbeSingleUrlAsync(string url, HttpClient client, CancellationToken ct)
    {
        if (_ProbeCache.TryGetValue(url, out var cached))
            return cached;

        var sw = Stopwatch.StartNew();
        var ttfbSw = new Stopwatch();

        try
        {
            // Phase 1: HEAD 请求获取元数据
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            // 部分服务器不支持 HEAD，降级为 Range GET
            if (response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.Forbidden)
            {
                response.Dispose();
                request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new RangeHeaderValue(0, BandwidthProbeBytes - 1);
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
            }

            response.EnsureSuccessStatusCode();
            var latencyMs = sw.ElapsedMilliseconds;

            // 解析文件大小
            var fileSize = response.Content.Headers.ContentLength ?? 0L;
            if (response.StatusCode == HttpStatusCode.PartialContent &&
                response.Content.Headers.ContentRange?.Length is { } rangeLength)
            {
                fileSize = rangeLength;
            }

            var supportRange = response.Headers.AcceptRanges.Contains("bytes") ||
                               response.StatusCode == HttpStatusCode.PartialContent;
            var eTag = response.Headers.ETag?.Tag?.Trim('"') ?? "NO_ETAG";

            // Phase 2: 带宽探测 (仅当支持 Range 时)
            double estimatedBandwidth = 0;
            long ttfbMs = latencyMs;

            if (supportRange && fileSize > BandwidthProbeBytes)
            {
                response.Dispose();
                (estimatedBandwidth, ttfbMs) = await _ProbeBandwidthAsync(url, client, ct).ConfigureAwait(false);
            }
            else if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                // 已有部分数据，可以估算
                ttfbSw.Start();
                await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                var buffer = new byte[BandwidthProbeBytes];
                var bytesRead = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
                ttfbSw.Stop();
                ttfbMs = ttfbSw.ElapsedMilliseconds;

                if (bytesRead > 0 && ttfbMs > 0)
                    estimatedBandwidth = bytesRead * 1000.0 / ttfbMs;
            }

            sw.Stop();
            var result = new ProbeResult(url, true, latencyMs, ttfbMs, fileSize, supportRange, eTag, estimatedBandwidth);
            _ProbeCache.TryAdd(url, result);
            return result;
        }
        catch
        {
            sw.Stop();
            var result = new ProbeResult(url, false, sw.ElapsedMilliseconds, sw.ElapsedMilliseconds, 0, false, "", 0);
            _ProbeCache.TryAdd(url, result);
            return result;
        }
    }

    /// <summary>
    /// 单独的带宽探测请求
    /// </summary>
    private static async Task<(double BandwidthBps, long TtfbMs)> _ProbeBandwidthAsync(
        string url, HttpClient client, CancellationToken ct)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(0, BandwidthProbeBytes - 1);

            var sw = Stopwatch.StartNew();
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return (0, sw.ElapsedMilliseconds);

            var ttfbMs = sw.ElapsedMilliseconds;

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var buffer = new byte[BandwidthProbeBytes];
            var totalRead = 0;

            while (totalRead < BandwidthProbeBytes)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(totalRead, BandwidthProbeBytes - totalRead), ct)
                    .ConfigureAwait(false);
                if (read == 0) break;
                totalRead += read;
            }

            sw.Stop();
            var downloadTimeMs = sw.ElapsedMilliseconds - ttfbMs;
            var bandwidth = downloadTimeMs > 0 ? totalRead * 1000.0 / downloadTimeMs : totalRead * 10.0;

            return (bandwidth, ttfbMs);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>
    /// 清除指定URL的缓存
    /// </summary>
    public static void InvalidateCache(string url) => _ProbeCache.TryRemove(url, out _);

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public static void ClearCache() => _ProbeCache.Clear();
}