using System.Collections.Generic;
using PCL.Core.App.Configuration.NTraffic;

namespace PCL.Core.App.Configuration.Impl;

public class FallbackTrafficCenter : SyncTrafficCenter
{
    /// <summary>
    /// 回滚列表，需自行确保有序。
    /// </summary>
    public required IEnumerable<TrafficCenter> FallbackList { get; init; }

    protected override void OnTrafficSync<TInput, TOutput>(PreviewTrafficEventArgs<TInput, TOutput> e)
    {
        foreach (var current in FallbackList)
        {
            current.Request(e);
            if (!e.HasOutput) continue;
            // 非 Write: 回滚到 output 有值为止
            if (e.Access != TrafficAccess.Write) break;
            // Write: 回滚到 output 有值且不为初始值为止
            if (!e.IsInitialOutput) break;
        }
    }
}
