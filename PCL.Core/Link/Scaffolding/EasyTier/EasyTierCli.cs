using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PCL.Core.Utils;

namespace PCL.Core.Link.Scaffolding.EasyTier;

/// <summary>
///     EasyTier CLI 的执行结果。
/// </summary>
/// <param name="ExitCode">进程退出码。</param>
/// <param name="StandardOutput">标准输出。</param>
/// <param name="StandardError">标准错误。</param>
public sealed record EasyTierCliResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    /// <summary>合并后的输出，便于日志与 JSON 解析。</summary>
    public string Output => StandardOutput + StandardError;
}

/// <summary>
///     统一构造和执行 <c>easytier-cli</c>。
///     所有平台路径、可执行文件扩展名与 Windows 专属进程选项都集中在这里。
/// </summary>
public static class EasyTierCli
{
    /// <summary>
    ///     创建指向当前平台 EasyTier CLI 的进程启动信息。
    /// </summary>
    public static ProcessStartInfo CreateStartInfo(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = EasyTierMetadata.CliPath,
            WorkingDirectory = EasyTierMetadata.InstallRoot,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = Encoding.UTF8
        };

        // WindowStyle 与 ErrorDialog 是 Windows 专属属性；Unix 上保持默认值。
        if (EasyTierMetadata.Package.Os == PlatformKind.Windows)
        {
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.ErrorDialog = false;
        }

        return startInfo;
    }

    /// <summary>
    ///     执行 EasyTier CLI 并等待退出。
    /// </summary>
    /// <param name="arguments">命令行参数。</param>
    /// <param name="timeout">超时时间；超时会终止进程并抛出 <see cref="TimeoutException" />。</param>
    /// <param name="cancellationToken">外部取消令牌。</param>
    /// <exception cref="TimeoutException">进程在超时前没有退出。</exception>
    /// <exception cref="OperationCanceledException">外部取消。</exception>
    public static async Task<EasyTierCliResult> RunAsync(
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process();
        process.StartInfo = CreateStartInfo(arguments);

        try
        {
            if (!process.Start())
                throw new InvalidOperationException("无法启动 EasyTier CLI");

            process.StandardInput.Close();

            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _TryKill(process);
                throw new TimeoutException(
                    $"EasyTier CLI 执行超时（{timeout.TotalMilliseconds:0} ms）：{arguments}");
            }
            catch (OperationCanceledException)
            {
                _TryKill(process);
                throw;
            }

            var standardOutput = await standardOutputTask.ConfigureAwait(false);
            var standardError = await standardErrorTask.ConfigureAwait(false);

            return new EasyTierCliResult(process.ExitCode, standardOutput, standardError);
        }
        catch (Exception ex) when (ex is not TimeoutException and not OperationCanceledException)
        {
            LogWrapper.Error(ex, "EasyTier CLI", $"执行失败：{arguments}");
            throw;
        }
    }

    /// <summary>
    ///     执行 EasyTier CLI 并把输出解析为 JSON。
    ///     退出码非 0、输出为空或解析失败时返回 <c>default</c>。
    /// </summary>
    public static async Task<T?> RunJsonAsync<T>(
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(arguments, timeout, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(result.Output, JsonCompat.SerializerOptions);
        }
        catch (JsonException ex)
        {
            LogWrapper.Warn(ex, "EasyTier CLI", $"无法解析 JSON 输出：{result.Output}");
            return default;
        }
    }

    private static void _TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "EasyTier CLI", "终止超时进程失败");
        }
    }
}