using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App.RemoteInfo.Sources;
using PCL.Core.Logging;

namespace PCL.Core.App.RemoteInfo;

/// <summary>
/// 管理多个更新源，尝试找到可用源并调用。
/// </summary>
public sealed class SourceController
{
    private readonly List<IUpdateSource> _availableSources;
    
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// 初始化并过滤出可用的更新源。
    /// </summary>
    public SourceController(IEnumerable<IUpdateSource> sources)
    {
        _availableSources = sources
            .Where(s => s.IsAvailable)
            .ToList();
    }

    /// <summary>
    /// 尝试使用当前源处理操作，若失败则遍历其他可用源直至成功或无可用源。
    /// </summary>
    /// <param name="action">指定操作</param>
    /// <typeparam name="T">返回类型</typeparam>
    /// <returns>操作返回值；若所有更新源均不可用，则返回 <c>default(T)</c>。</returns>
    private async Task<T?> _TryFindSourceAsync<T>(Func<IUpdateSource, Task<T>> action)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var source in _availableSources)
            {
                try
                {
                    var res = await action(source).ConfigureAwait(false);
                    _LogInfo($"源 {source.SourceName} 处理成功");
                    return res;
                }
                catch (Exception ex)
                {
                    _LogWarning($"源 {source.SourceName} 不可用，使用下一个源", ex);
                }
            }
            
            _LogWarning("所有更新源均不可用");
            return default;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 检查是否有新版本并返回结果。
    /// </summary>
    public Task<VersionData?> GetLatestVersionAsync() => 
        _TryFindSourceAsync(s => s.GetLatestVersionAsync());

    /// <summary>
    /// 获取公告列表。
    /// </summary>
    public Task<AnnouncementsListModel?> GetAnnouncementListAsync() =>
        _TryFindSourceAsync(s => s.GetAnnouncementAsync());

    /// <summary>
    /// 使用可用源下载到指定路径。
    /// </summary>
    public async Task<bool> DownloadAsync(string outputPath) => 
        await _TryFindSourceAsync(s => s.DownloadAsync(outputPath)).ConfigureAwait(false);

    #region Logger Wrapper

    private void _LogInfo(string msg)
    {
        LogWrapper.Info("Update", msg);
    }

    private void _LogWarning(string msg, Exception? ex = null)
    {
        LogWrapper.Warn(ex, "Update", msg);
    }

    private void _LogError(string msg, Exception? ex = null)
    {
        LogWrapper.Error(ex, "Update", msg);
    }

    private void _LogTrace(string msg)
    {
        LogWrapper.Trace("Update", msg);
    }

    #endregion
}
