using PCL.Core.Net.Downloader.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Net.Downloader.Network;

public class MetadataProber(TimeSpan timeout)
{
    public async Task<(long FileSize, bool SupportRange, List<MirrorInfo> SortedMirrors)> ProbeAsync(List<string> urls,
        HttpClient client)
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
            // Log warning about inconsistent metadata among mirrors
        }

        var finalFileSize = consensusGroup[0].FileSize;
        var finalSupprotRange = consensusGroup.Any(r => r.SupportRange);

        var sortedMirrors = consensusGroup
            .OrderBy(r => r.LatencyMs)
            .Select((r, index) => new MirrorInfo
            {
                Url = r.Url,
                IsAlive = true,
                LatencyMs = r.LatencyMs,
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

    private async Task<ProbeResult> _ProbeSingleUrlAsync(string url, HttpClient client)
    {
        var sw = Stopwatch.StartNew();
        using var ctx = new CancellationTokenSource(timeout);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctx.Token)
                .ConfigureAwait(false);

            if (response is
                {
                    IsSuccessStatusCode: false,
                    StatusCode: HttpStatusCode.MethodNotAllowed or HttpStatusCode.Forbidden
                })
            {
                request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new RangeHeaderValue(0, 0);

                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctx.Token)
                    .ConfigureAwait(false);
            }

            response.EnsureSuccessStatusCode();
            sw.Stop();

            var fileSize = response.Content.Headers.ContentLength ?? 0L;

            if (response is
                {
                    StatusCode: HttpStatusCode.PartialContent,
                    Content.Headers.ContentRange: not null
                })
            {
                fileSize = response.Content.Headers.ContentRange.Length ?? fileSize;
            }

            var supportRange = response.Headers.AcceptRanges.Contains("bytes") ||
                               response.StatusCode == HttpStatusCode.PartialContent;

            var eTag = response.Headers.ETag?.Tag?.Trim('"') ?? "NO_ETAG";

            return new ProbeResult(url, true, sw.ElapsedMilliseconds, fileSize, supportRange, eTag);
        }
        catch
        {
            sw.Stop();
            return new ProbeResult(url, false, sw.ElapsedMilliseconds, 0, false, string.Empty);
        }
    }
}