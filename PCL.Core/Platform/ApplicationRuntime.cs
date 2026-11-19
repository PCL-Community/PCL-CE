using System;
using System.Threading;
using PCL.Core.Logging;

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

    /// <summary>从服务容器解析服务；运行时未初始化或服务未注册时返回 <see langword="null" />。</summary>
    public static T? GetService<T>() where T : class
    {
        var current = Volatile.Read(ref _current);
        return (T?)current?.Services?.GetService(typeof(T));
    }

    public static T GetRequiredService<T>() where T : class
    {
        return GetService<T>()
            ?? throw new InvalidOperationException($"必需的服务未注册：{typeof(T).FullName}");
    }

    public static void Initialize(ApplicationServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (Interlocked.CompareExchange(ref _current, services, null) is not null)
            throw new InvalidOperationException("应用运行时只能初始化一次");
    }

    /// <summary>幂等释放应用运行时持有的平台与 UI 服务。</summary>
    public static void Shutdown()
    {
        var services = Interlocked.Exchange(ref _current, null);
        if (services is null) return;

        try
        {
            services.Ui.Dispose();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "释放 UI 服务失败");
        }

        try
        {
            services.Platform.Dispose();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "释放平台服务失败");
        }

        try
        {
            (services.Services as IDisposable)?.Dispose();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "释放服务容器失败");
        }
    }

    /// <summary>仅供测试重置运行时状态。</summary>
    internal static void ResetForTests()
    {
        Volatile.Write(ref _current, null);
    }
}