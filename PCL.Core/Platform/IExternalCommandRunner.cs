using System;
using System.Collections.Generic;
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

    /// <summary>启动一个持续读取标准输出的命令，用于监视类场景。 返回的对象释放时会终止并释放底层进程。</summary>
    IDisposable Watch(
        string fileName,
        IReadOnlyList<string> arguments,
        Action<string> onOutput,
        Action? onExit = null);
}