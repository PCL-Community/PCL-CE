using System;

namespace PCL.Core.Platform;

/// <summary>
///     应用容器抽象。WPF 的 <c>Application</c> 与未来的 Avalonia 容器由 UI 层各自实现。
/// </summary>
public interface IApplicationHost
{
    /// <summary>主窗口。由 <c>MainWindowService</c> 在窗口创建后赋值。</summary>
    IWindowHost? MainWindow { get; set; }

    /// <summary>运行应用消息循环，返回退出码。</summary>
    int Run();

    /// <summary>按资源键查找资源，未找到时返回 <c>null</c>。</summary>
    object? TryFindResource(string key);

    /// <summary>按资源键查找资源，未找到时抛出异常。</summary>
    object? FindResource(string key);

    /// <summary>写入应用级资源。</summary>
    void SetResource(object key, object value);

    /// <summary>请求关闭应用。</summary>
    void Shutdown();

    /// <summary>应用启动完成事件。</summary>
    event Action? Startup;

    /// <summary>UI 线程未处理异常事件。</summary>
    event Action<Exception>? DispatcherUnhandledException;
}