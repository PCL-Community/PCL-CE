using System;
using System.Threading;

namespace PCL.Core.Platform;

/// <summary>应用运行时的唯一服务入口。只在启动时初始化一次。</summary>
public static class ApplicationRuntime
{
    private static ApplicationServices? _current;

    public static ApplicationServices Current
    {
        get
        {
            var current = Volatile.Read(ref _current);
            return current ?? throw new InvalidOperationException("应用运行时尚未初始化");
        }
    }

    public static bool IsInitialized => Volatile.Read(ref _current) is not null;

    public static void Initialize(ApplicationServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (Interlocked.CompareExchange(ref _current, services, null) is not null)
            throw new InvalidOperationException("应用运行时只能初始化一次");
    }

    /// <summary>仅供测试重置运行时状态。</summary>
    internal static void ResetForTests()
    {
        Volatile.Write(ref _current, null);
    }
}