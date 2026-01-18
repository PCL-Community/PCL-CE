using System;
using System.Collections.Generic;
using PCL.Core.App.Configuration.NTraffic;
using PCL.Core.Logging;

namespace PCL.Core.App.Configuration.Impl;

/// <summary>
/// 将传入上下文作为配置文件目录动态加载的物流实现。
/// </summary>
public class DynamicCacheTrafficCenter : SyncTrafficCenter
{
    private readonly Dictionary<object, TrafficCenter> _cache = [];

    /// <summary>
    /// 物流中心工厂。在没有匹配的上下文实例时将被调用，以创建新的上下文实例。
    /// </summary>
    public required Func<object, TrafficCenter> TrafficCenterFactory { get; set; }

    protected override void OnTrafficSync<TInput, TOutput>(PreviewTrafficEventArgs<TInput, TOutput> e)
    {
        var context = e.Context;
        if (context == null) return;
        _cache.TryGetValue(context, out var value);
        if (value == null)
        {
            try
            {
                value = TrafficCenterFactory(context);
                _cache[context] = value;
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Config", "Failed to invoke TrafficCenter factory");
            }
        }
        value?.Request(e);
    }

    protected override void OnStop()
    {
        foreach (var item in _cache.Values) item.Stop();
        _cache.Clear();
    }

    public bool InvalidateCache(object context)
    {
        var result = _cache.TryGetValue(context, out var center);
        if (result)
        {
            center?.Stop();
            _cache.Remove(context);
        }
        return result;
    }
}
