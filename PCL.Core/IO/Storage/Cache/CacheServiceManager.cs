using PCL.Core.App.IoC;
using System;
using System.Threading.Tasks;

namespace PCL.Core.IO.Storage.Cache;

[LifecycleScope("global_cache", "磁盘缓存服务")]
[LifecycleService(LifecycleState.Loading, Priority = 500)]
public partial class CacheServiceManager
{
    private static CacheService? _service;

    /// <summary>
    /// 获取当前缓存服务实例，如果尚未初始化则为 null
    /// </summary>
    public static CacheService Current => _service ?? throw new InvalidOperationException("Cache service is not initialized yet.");

    [LifecycleStart]
    private static async Task _StartAsync()
    {
        Context.Debug("正在初始化缓存服务...");

        _service = new CacheService();
        await _service.InitializeAsync().ConfigureAwait(false);

        Context.Info("缓存服务初始化成功");
    }

    [LifecycleStop]
    private static async Task _StopAsync()
    {
        Context.Debug("正在停止缓存服务...");

        if (_service != null)
        {
            await _service.DisposeAsync().ConfigureAwait(false);
            _service = null;
        }

        Context.Info("缓存服务已停止");
    }
}
