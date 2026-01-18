using PCL.Core.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Net.Downloader;

/// <summary>
/// 下载分段
/// </summary>
/// <param name="uri">下载链接</param>
/// <param name="path">保存路径</param>
/// <param name="start">分段起始位置</param>
/// <param name="end">分段结束位置（可选）</param>
public class DownloadSegment(Uri uri, string path, long start, long? end)
{
    public long Start { get; } = start;
    public long? End { get; set; } = end;
    public long Downloaded { get; private set; }
    public DownloadSegmentStatus Status { get; private set; } = DownloadSegmentStatus.WaitingStart;
    public long RemainingBytes => (End ?? 0) - (Start + Downloaded);
    private Uri _currentUri = uri;

    /// <summary>
    /// 更新下载链接
    /// </summary>
    /// <param name="newUri">新的下载链接</param>
    public void UpdateUri(Uri newUri) => _currentUri = newUri;

    /// <summary>
    /// 异步下载分段
    /// </summary>
    /// <param name="client">HTTP 客户端</param>
    /// <param name="token">取消令牌</param>
    /// <param name="progress">下载进度回调</param>
    public async Task DownloadAsync(HttpClient client, CancellationToken token, IProgress<long>? progress = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _currentUri);
        if (End is not null)
        {
            request.Headers.Range = new RangeHeaderValue(Start + Downloaded, End);
        }
        else if (Start + Downloaded > 0)
        {
            request.Headers.Range = new RangeHeaderValue(Start + Downloaded, null);
        }

        var rangeInfo = request.Headers.Range is not null && request.Headers.Range.Ranges.Count > 0
            ? $"Range: {request.Headers.Range.Ranges.First().From}-{request.Headers.Range.Ranges.First().To}"
            : "No Range";

        LogWrapper.Trace("Downloader", $"开始下载分段: {Start}-{End}, {rangeInfo}, URI: {_currentUri}");
        Status = DownloadSegmentStatus.Running;

        try
        {
            LogWrapper.Debug("Downloader", $"发送请求: {_currentUri}, {rangeInfo}");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);

            LogWrapper.Debug("Downloader", $"收到响应: {response.StatusCode}, URI: {_currentUri}");

            if (request.Headers.Range is not null && response.StatusCode == HttpStatusCode.OK)
            {
                LogWrapper.Warn("Downloader", $"服务器不支持范围请求，返回200 OK: {_currentUri}");
                throw new DownloadRangeNotSupportedException(_currentUri);
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            LogWrapper.Trace("Downloader", $"打开文件流: {path}, 偏移量: {Start + Downloaded}");
            await using var fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write,
                FileShare.ReadWrite, bufferSize: 4096, FileOptions.Asynchronous);
            fileStream.Seek(Start + Downloaded, SeekOrigin.Begin);

            var buffer = new byte[16384];
            int read;
            long totalRead = 0;

            LogWrapper.Trace("Downloader", $"开始读取数据: {Start}-{End}, URI: {_currentUri}");
            while ((read = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                Downloaded += read;
                totalRead += read;
                progress?.Report(read);

                // 每下载1MB记录一次进度
                if (totalRead % (1024 * 1024) < read)
                {
                    LogWrapper.Trace("Downloader",
                        $"分段下载进度: {Start}-{End}, 已下载: {Downloaded} 字节, 剩余: {RemainingBytes} 字节");
                }
            }

            LogWrapper.Debug("Downloader", $"分段下载完成: {Start}-{End}, 共下载: {totalRead} 字节, URI: {_currentUri}");
            Status = DownloadSegmentStatus.Success;
        }
        catch (OperationCanceledException)
        {
            LogWrapper.Warn("Downloader", $"分段下载被取消: {Start}-{End}, URI: {_currentUri}");
            Status = DownloadSegmentStatus.Cancelled;
            throw;
        }
        catch (DownloadRangeNotSupportedException)
        {
            LogWrapper.Error("Downloader", $"分段下载失败，服务器不支持范围请求: {Start}-{End}, URI: {_currentUri}");
            Status = DownloadSegmentStatus.Failed;
            throw;
        }
        catch (HttpRequestException ex)
        {
            LogWrapper.Error(ex, "Downloader", $"分段下载失败，HTTP请求错误: {Start}-{End}, URI: {_currentUri}");
            Status = DownloadSegmentStatus.Failed;
            throw;
        }
        catch (IOException ex)
        {
            LogWrapper.Error(ex, "Downloader", $"分段下载失败，IO错误: {Start}-{End}, 文件: {path}");
            Status = DownloadSegmentStatus.Failed;
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Downloader", $"分段下载失败，未知错误: {Start}-{End}, URI: {_currentUri}");
            Status = DownloadSegmentStatus.Failed;
            throw;
        }
    }
}