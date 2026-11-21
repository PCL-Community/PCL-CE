using System;
using System.Threading;
using PCL.Core.Platform;

namespace PCL.Core.Minecraft.Watcher;

/// <summary>启动一次游戏会话监视所需的参数。</summary>
public sealed class GameSessionOptions
{
    /// <summary>实例与版本元数据。</summary>
    public required GameSessionMetadata Metadata { get; init; }

    /// <summary>游戏窗口标题模板，支持 <c>{date}</c> 与 <c>{time}</c>。</summary>
    public string? WindowTitleTemplate { get; init; }

    /// <summary>是否记录并发布实时日志。</summary>
    public bool CaptureLiveLog { get; init; }

    /// <summary>jstack 可执行文件路径。</summary>
    public string? StackDumpToolPath { get; init; }

    /// <summary>找到游戏窗口后是否请求最大化。</summary>
    public bool MaximizeWindow { get; init; }

    /// <summary>加载进度回调，参数范围为 0 至 1。</summary>
    public Action<double>? ReportProgress { get; init; }

    /// <summary>启动流程的取消令牌。触发后会话进入 <see cref="GameSessionState.Canceled" />。</summary>
    public CancellationToken AbortToken { get; init; }

    /// <summary>窗口轮询间隔。</summary>
    public TimeSpan WindowPollInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>窗口标题刷新间隔。</summary>
    public TimeSpan WindowTitleRefreshInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>找到窗口后等待多久再请求最大化。</summary>
    public TimeSpan MaximizeDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>崩溃分析可读取的最近日志行数。</summary>
    public int RecentLogCapacity { get; init; } = 500;

    /// <summary>导出运行栈时每次尝试之间的等待时间。</summary>
    public TimeSpan StackDumpInterval { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>窗口交互实现。</summary>
    public IWindowInterop? WindowInterop { get; init; }

    /// <summary>外部命令执行器。</summary>
    public IExternalCommandRunner? CommandRunner { get; init; }
}