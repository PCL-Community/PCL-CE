using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Platform;

/// <summary>外部命令的一次性执行结果。</summary>
public readonly record struct ExternalCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>持续运行的外部命令会话。</summary>
public interface IExternalCommandSession : IDisposable
{
    /// <summary>命令的标准输出流。</summary>
    Stream StandardOutput { get; }

    /// <summary>命令的标准错误流。</summary>
    Stream StandardError { get; }

    /// <summary>命令是否已经退出。</summary>
    bool HasExited { get; }

    /// <summary>命令退出码；命令尚未退出时返回 -1。</summary>
    int ExitCode { get; }

    /// <summary>等待命令退出。</summary>
    Task WaitForExitAsync(CancellationToken cancellationToken = default);

    /// <summary>终止命令及其子进程。</summary>
    void Kill();
}

/// <summary>平台外部命令执行入口。UI 层不得直接创建 <see cref="System.Diagnostics.Process" />。</summary>
public interface IExternalCommandRunner
{
    /// <summary>判断命令是否存在于当前 PATH 中。</summary>
    bool ExistsOnPath(string command);

    /// <summary>执行外部命令并等待退出。</summary>
    /// <param name="fileName">程序文件名或完整路径。</param>
    /// <param name="arguments">按参数边界拆分的参数列表，不进行 shell 拼接。</param>
    /// <param name="timeout">超时时间；小于等于零表示不设置超时。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <exception cref="TimeoutException">命令超时后被终止。</exception>
    Task<ExternalCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>启动一个持续运行的外部命令，由调用方读取其标准输出和标准错误。</summary>
    /// <param name="fileName">程序文件名或完整路径。</param>
    /// <param name="arguments">按参数边界拆分的参数列表，不进行 shell 拼接。</param>
    /// <returns>可用于读取输出、等待退出和终止进程的会话。</returns>
    IExternalCommandSession Start(
        string fileName,
        IReadOnlyList<string> arguments);

    /// <summary>启动一个持续读取标准输出的命令，用于监视类场景。 返回的对象释放时会终止并释放底层进程。</summary>
    IDisposable Watch(
        string fileName,
        IReadOnlyList<string> arguments,
        Action<string> onOutput,
        Action? onExit = null);
}