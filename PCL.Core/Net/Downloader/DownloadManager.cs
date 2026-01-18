using PCL.Core.Logging;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Net.Downloader;

/// <summary>
/// 下载管理器
/// </summary>
public class DownloadManager
{
    private readonly HttpClient _client;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly IMirrorSelector _mirrorSelector;
    public int MaxParallelSegmentsPerTask { get; set; } = 4;

    private static readonly ResiliencePropertyKey<DownloadTask> _TaskKey = new("DownloadTask");

    /// <summary>
    /// 构造下载管理器
    /// </summary>
    /// <param name="selector">镜像源选择器</param>
    public DownloadManager(IMirrorSelector selector)
    {
        _client = NetworkService.GetClient();
        _mirrorSelector = selector;

        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<IOException>()
                    .Handle<SocketException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    if (args.Context.Properties.TryGetValue(_TaskKey, out var task))
                    {
                        task.RotateMirror();
                    }

                    return default;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(45))
            .Build();
    }

    /// <summary>
    /// 下载任务
    /// </summary>
    /// <param name="task">下载任务</param>
    /// <param name="token">取消令牌</param>
    public async Task DownloadAsync(DownloadTask task, CancellationToken token)
    {
        LogWrapper.Debug("Downloader", $"开始下载任务: {task.ActiveUri} -> {task.TargetPath}");

        await task.PrepareAsync(_client, _mirrorSelector, token).ConfigureAwait(false);

        using var taskSemaphore = new SemaphoreSlim(MaxParallelSegmentsPerTask);
        var activeTasks = new List<Task>();

        LogWrapper.Debug("Downloader", $"初始化分段: {task.Segments.Count} 个分段");
        lock (task.Segments)
        {
            foreach (var segment in task.Segments)
            {
                LogWrapper.Trace("Downloader", $"执行分段: {segment.Start}-{segment.End}");
                activeTasks.Add(_ExecuteSegmentWithResilienceAsync(segment, task, taskSemaphore, token));
            }
        }

        while (activeTasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(activeTasks).ConfigureAwait(false);
            activeTasks.Remove(completedTask);

            await completedTask.ConfigureAwait(false);

            while (activeTasks.Count < MaxParallelSegmentsPerTask)
            {
                lock (task)
                {
                    var newSeg = task.TrySplitSegment();
                    if (newSeg is not null)
                    {
                        LogWrapper.Trace("Downloader", $"添加新分段: {newSeg.Start}-{newSeg.End}");
                        activeTasks.Add(_ExecuteSegmentWithResilienceAsync(newSeg, task, taskSemaphore, token));
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        LogWrapper.Debug("Downloader", $"下载任务完成: {task.ActiveUri} -> {task.TargetPath}");
    }
    
    /// <summary>
    /// 使用弹性策略执行分段下载
    /// </summary>
    /// <param name="segment">下载分段</param>
    /// <param name="parent">所属下载任务</param>
    /// <param name="semaphore">并发信号量</param>
    /// <param name="token">取消令牌</param>
    private async Task _ExecuteSegmentWithResilienceAsync(
        DownloadSegment segment,
        DownloadTask parent,
        SemaphoreSlim semaphore,
        CancellationToken token)
    {
        LogWrapper.Trace("Downloader", $"开始执行分段: {segment.Start}-{segment.End}");
        await semaphore.WaitAsync(token).ConfigureAwait(false);

        try
        {
            var context = ResilienceContextPool.Shared.Get(token);
            context.Properties.Set(_TaskKey, parent);

            try
            {
                await _resiliencePipeline.ExecuteAsync(async (ctx) =>
                {
                    try
                    {
                        segment.UpdateUri(parent.ActiveUri);
                        LogWrapper.Trace("Downloader",
                            $"正在下载分段: {segment.Start}-{segment.End}, URI: {parent.ActiveUri}");
                        await segment.DownloadAsync(_client, ctx.CancellationToken).ConfigureAwait(false);
                        LogWrapper.Trace("Downloader", $"分段下载成功: {segment.Start}-{segment.End}");
                    }
                    catch (DownloadRangeNotSupportedException)
                    {
                        parent.SupportsRange = false;
                        LogWrapper.Warn("Downloader", $"服务器不支持范围请求: {parent.ActiveUri}");
                        throw;
                    }
                    catch (Exception ex) when (ex is HttpRequestException or IOException)
                    {
                        LogWrapper.Warn(ex, "Downloader", $"镜像下载失败: {parent.ActiveUri}, 将切换镜像");
                        throw new MirrorFailedException(parent.ActiveUri, ex);
                    }
                }, context).ConfigureAwait(false);
            }
            catch (DownloadRangeNotSupportedException)
            {
                LogWrapper.Error("Downloader", "Server not support range.");

                if (segment.Start == 0)
                {
                    LogWrapper.Trace("Downloader", "分段0下载成功，跳过其他分段");
                    return;
                }

                throw;
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Downloader", $"分段下载失败: {segment.Start}-{segment.End}");
                throw;
            }
            finally
            {
                ResilienceContextPool.Shared.Return(context);
            }
        }
        finally
        {
            semaphore.Release();
            LogWrapper.Trace("Downloader", $"分段执行完成: {segment.Start}-{segment.End}");
        }
    }
}