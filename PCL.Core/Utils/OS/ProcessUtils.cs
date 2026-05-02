using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.UI;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Utils.OS;

public static class ProcessUtils
{
    const string ModelName = "System";

    /// <summary>
    /// 前台运行文件。
    /// </summary>
    /// <param name="fileName">文件名。可以为“notepad”等缩写。</param>
    /// <param name="arguments">运行参数。</param>
    public static void ShellOnly(string fileName, string[]? arguments = null)
    {
        try
        {
            fileName = PathUtils.ToShortenPath(fileName);
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = true
            };
            var realArgs = arguments ?? Array.Empty<string>();

            foreach (var arg in realArgs)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var program = new Process();
            program.StartInfo = startInfo;
            LogWrapper.Info(ModelName, $"执行外部命令：{fileName} {string.Join(' ', realArgs)}");
            program.Start();
        }
        catch (Exception ex)
        {
            _ReportToUser($"打开文件或程序失败：{fileName}", LogLevel.Error, ex);
        }
    }

    /// <summary>
    /// 前台运行文件并返回返回值。
    /// </summary>
    /// <param name="fileName">文件名。可以为“notepad”等缩写。</param>
    /// <param name="arguments">运行参数。</param>
    /// <param name="timeout">等待该程序结束的最长时间（毫秒）。超时会返回 Result.Timeout。</param>
    public static async Task<Enums.ProcessReturnValues> ShellAndGetExitCodeAsync(string fileName,
        string arguments = "",
        int timeout = 1000000)
    {
        try
        {
            using var program = new Process();
            program.StartInfo.Arguments = arguments;
            program.StartInfo.FileName = fileName;
            LogWrapper.Info(ModelName, $"执行外部命令并等待返回码：{fileName} {arguments}");
            program.Start();

            var timeoutToken = new CancellationTokenSource(timeout);
            try
            {
                await program.WaitForExitAsync(timeoutToken.Token).ConfigureAwait(false);

            }
            catch (OperationCanceledException)
            {
                return Enums.ProcessReturnValues.Timeout;
            }

            return (Enums.ProcessReturnValues)program.ExitCode;
        }
        catch (Exception ex)
        {
            _ReportToUser($"执行命令失败：{fileName}", LogLevel.Error, ex);
            return Enums.ProcessReturnValues.Fail;
        }
    }

    public static Enums.ProcessReturnValues ShellAndGetExitCode(string fileName,
        string arguments = "",
        int timeout = 1000000)
        => ShellAndGetExitCodeAsync(fileName, arguments, timeout).GetAwaiter().GetResult();

    /// <summary>
    /// 静默运行文件并返回输出流字符串。执行失败会抛出异常。
    /// </summary>
    /// <param name="fileName">文件名。可以为“notepad”等缩写。</param>
    /// <param name="arguments">运行参数。</param>
    /// <param name="timeoutMs">等待该程序结束的最长时间（毫秒）。超时会抛出错误。</param>
    /// <param name="workingDirectory">工作目录。</param>
    public static async Task<string> ShellAndGetOutputAsync(string fileName,
        string arguments = "",
        int timeoutMs = 1000000,
        string? workingDirectory = null)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // 设置工作目录（如果提供）
        if (!string.IsNullOrEmpty(workingDirectory))
        {
            info.WorkingDirectory = workingDirectory.TrimEnd('\\');
        }

        LogWrapper.Info(ModelName, $"执行外部命令并等待返回结果：{fileName} {arguments}");

        using var program = new Process();
        program.StartInfo = info;
        program.Start();

        // 异步读取输出和错误流
        var stdOut = await program.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var stdErr = await program.StandardError.ReadToEndAsync().ConfigureAwait(false);

        var timeoutToken = new CancellationTokenSource(timeoutMs);
        try
        {
            await program.WaitForExitAsync(timeoutToken.Token).ConfigureAwait(false);

        }
        catch (OperationCanceledException)
        {
            // ignore
        }

        // 合并结果并返回
        var sb = new StringBuilder(stdOut.Length + stdErr.Length);
        sb.Append(stdOut);
        sb.Append(stdErr);
        return sb.ToString();
    }

    public static string ShellAndGetOutput(string fileName,
    string arguments = "",
    int timeoutMs = 1000000,
    string? workingDirectory = null)
    => ShellAndGetOutputAsync(fileName, arguments, timeoutMs, workingDirectory).GetAwaiter().GetResult();


    private static void _ReportToUser(string msg, LogLevel level, Exception? ex = null)
    {
        switch (level)
        {
            case LogLevel.Trace:
                LogWrapper.Trace(ModelName, msg);
                break;
            case LogLevel.Debug:
                LogWrapper.Debug(ModelName, msg);
                break;
            case LogLevel.Info:
                LogWrapper.Info(ModelName, msg);
                break;
            case LogLevel.Warning:
                LogWrapper.Warn(ModelName, msg);
                MsgBoxWrapper.Show(msg, "警告", MsgBoxTheme.Warning);
                break;
            case LogLevel.Error:
                LogWrapper.Error(ex, ModelName, msg);
                MsgBoxWrapper.Show(msg, "错误", MsgBoxTheme.Error);
                break;
            case LogLevel.Fatal:
                LogWrapper.Fatal(ex, ModelName, msg);
                MsgBoxWrapper.Show(msg, "异常", MsgBoxTheme.Error);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }
    }
}