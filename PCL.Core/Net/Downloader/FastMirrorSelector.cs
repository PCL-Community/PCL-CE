using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Net.Downloader;

public record MirrorInfo(Uri Uri, long Latency = -1, bool IsAvailable = true);

/// <summary>
/// 最快镜像选择器
/// </summary>
/// <param name="client">用于测试镜像的 HttpClient 实例</param>
public class FastMirrorSelector(HttpClient client) : IMirrorSelector
{
    /// <inheritdoc />
    public async Task<Uri> GetBestMirrorAsync(IEnumerable<Uri> uris, CancellationToken token)
    {
        var urisArray = uris as Uri[] ?? uris.ToArray();

        var tasks = urisArray.Select(async uri =>
        {
            try
            {
                var sw = Stopwatch.StartNew();

                using var response = await client.SendAsync(
                    new HttpRequestMessage(HttpMethod.Head, uri),
                    HttpCompletionOption.ResponseHeadersRead,
                    token).ConfigureAwait(false);

                sw.Stop();

                return new MirrorInfo(uri, sw.ElapsedMilliseconds, response.IsSuccessStatusCode);
            }
            catch
            {
                return new MirrorInfo(uri, IsAvailable: false);
            }
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var best = results.Where(r => r.IsAvailable)
            .OrderBy(r => r.Latency)
            .FirstOrDefault();

        return best?.Uri ?? urisArray[0];
    }
}
