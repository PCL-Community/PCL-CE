using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Minecraft.Launch.Utils;

namespace PCL.Core.Minecraft.Launch.Services;

/// <summary>
/// 负责执行自定义启动命令和生成启动脚本的服务
/// </summary>
public class CustomLaunchService(IMcInstance instance, JavaInfo selectedJava, string launchArg) {
    private readonly IMcInstance _instance = instance ?? throw new ArgumentNullException(nameof(instance));
    private readonly JavaInfo _selectedJava = selectedJava ?? throw new ArgumentNullException(nameof(selectedJava));
    private readonly string _launchArg = launchArg ?? throw new ArgumentNullException(nameof(launchArg));
    private readonly CustomEnvReplacer _envReplacer = new(instance, selectedJava);

    /// <summary>
    /// 执行自定义启动流程，包括生成启动脚本和执行自定义命令
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ExecuteCustomCommandAsync(CancellationToken cancellationToken = default) {
        try {
            // 准备自定义命令
            var customCommands = PrepareCustomCommands();

            // 并行执行启动脚本生成和自定义命令执行
            var tasks = new Task[] {
                GenerateLaunchScriptAsync(customCommands.Global, customCommands.Version, cancellationToken),
                ExecuteCustomCommandsAsync(customCommands.Global, customCommands.Version, cancellationToken)
            };

            await Task.WhenAll(tasks);
        } catch (OperationCanceledException) {
            LogWrapper.Info("Custom launch process was cancelled");
            throw;
        } catch (Exception ex) {
            LogWrapper.Error(ex, "Failed to execute custom launch process");
            throw;
        }
    }

    /// <summary>
    /// 准备全局和实例级别的自定义命令
    /// </summary>
    private CustomCommandInfo PrepareCustomCommands() {
        var globalCommand = Config.Launch.PreLaunchCommand;
        var versionCommand = Config.Instance.PreLaunchCommand[_instance.Path];

        return new CustomCommandInfo(
            Global: ProcessCustomCommand(globalCommand),
            Version: ProcessCustomCommand(versionCommand)
            );
    }

    /// <summary>
    /// 处理单个自定义命令，进行环境变量替换
    /// </summary>
    private string? ProcessCustomCommand(string? command) {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        return _envReplacer.ArgumentReplace(command, replaceTimeAndDate: true);
    }

    /// <summary>
    /// 异步生成启动脚本
    /// </summary>
    private async Task GenerateLaunchScriptAsync(string? globalCommand, string? versionCommand, CancellationToken cancellationToken) {
        try {
            var scriptContent = BuildLaunchScript(globalCommand, versionCommand);
            var scriptPath = Path.Combine(Basics.ExecutablePath, "PCL", "LatestLaunch.bat");
            var encoding = GetScriptEncoding();

            await Files.WriteFileAsync(
                scriptPath,
                McLaunchUtils.FilterAccessToken(scriptContent, 'F'),
                encoding: encoding,
                cancelToken: cancellationToken);

            LogWrapper.Debug($"Launch script generated at: {scriptPath}");
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "输出启动脚本失败");
        }
    }

    /// <summary>
    /// 构建启动脚本内容
    /// </summary>
    private string BuildLaunchScript(string? globalCommand, string? versionCommand) {
        var scriptBuilder = new StringBuilder();

        // 设置编码（Java 9+ 支持 UTF-8）
        if (_selectedJava.JavaMajorVersion > 8) {
            scriptBuilder.AppendLine("chcp 65001>nul");
        }

        // 基本设置
        scriptBuilder.AppendLine("@echo off");
        scriptBuilder.AppendLine($"title 启动 - {_instance.Name}");
        scriptBuilder.AppendLine("echo 游戏正在启动，请稍候。");
        scriptBuilder.AppendLine($"cd /D \"{_instance.IsolatedPath}\"");

        // 添加自定义命令
        if (!string.IsNullOrEmpty(globalCommand)) {
            scriptBuilder.AppendLine(globalCommand);
        }

        if (!string.IsNullOrEmpty(versionCommand)) {
            scriptBuilder.AppendLine(versionCommand);
        }

        // Java 启动命令
        scriptBuilder.AppendLine($"\"{_selectedJava.JavaExePath}\" {_launchArg}");

        // 结束提示
        scriptBuilder.AppendLine("echo 游戏已退出。");
        scriptBuilder.AppendLine("pause");

        return scriptBuilder.ToString();
    }

    /// <summary>
    /// 获取脚本编码
    /// </summary>
    private Encoding GetScriptEncoding() {
        return _selectedJava.JavaMajorVersion > 8 ? Encoding.UTF8 : Encoding.Default;
    }

    /// <summary>
    /// 异步执行自定义命令
    /// </summary>
    private async Task ExecuteCustomCommandsAsync(string? globalCommand, string? versionCommand, CancellationToken cancellationToken) {
        // 串行执行自定义命令以保持执行顺序
        if (!string.IsNullOrEmpty(globalCommand)) {
            await ExecuteSingleCustomCommandAsync(
                globalCommand,
                "全局自定义命令",
                Config.Launch.PreLaunchCommandWait,
                cancellationToken);
        }

        if (!string.IsNullOrEmpty(versionCommand)) {
            await ExecuteSingleCustomCommandAsync(
                versionCommand,
                "实例自定义命令",
                Config.Instance.PreLaunchCommandWait[_instance.Path],
                cancellationToken);
        }
    }

    /// <summary>
    /// 执行单个自定义命令
    /// </summary>
    private async Task ExecuteSingleCustomCommandAsync(
        string command,
        string commandType,
        bool waitForExit,
        CancellationToken cancellationToken) {
        McLaunchUtils.Log($"正在执行{commandType}：{command}");

        using var process = CreateCustomProcess(command);

        try {
            var startSuccess = process.Start();
            if (!startSuccess) {
                LogWrapper.Warn($"Failed to start {commandType}: {command}");
                return;
            }

            if (waitForExit) {
                await WaitForProcessExitAsync(process, cancellationToken);
            }

            LogWrapper.Debug($"{commandType} executed successfully");
        } catch (OperationCanceledException) {
            LogWrapper.Info($"{commandType} was cancelled");
            await TerminateProcessSafelyAsync(process);
            throw;
        } catch (Exception ex) {
            LogWrapper.Warn(ex, $"执行{commandType}失败");
            await TerminateProcessSafelyAsync(process);
        }
    }

    /// <summary>
    /// 创建自定义进程配置
    /// </summary>
    private Process CreateCustomProcess(string command) {
        return new Process {
            StartInfo = new ProcessStartInfo {
                FileName = "cmd.exe",
                Arguments = $"/c \"{command}\"",
                WorkingDirectory = _instance.Folder.Path,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            }
        };
    }

    /// <summary>
    /// 异步等待进程退出
    /// </summary>
    private static async Task WaitForProcessExitAsync(Process process, CancellationToken cancellationToken) {
        while (!process.HasExited && !cancellationToken.IsCancellationRequested) {
            await Task.Delay(100, cancellationToken); // 使用更合理的检查间隔
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// 安全地终止进程
    /// </summary>
    private static async Task TerminateProcessSafelyAsync(Process process) {
        try {
            if (!process.HasExited) {
                McLaunchUtils.Log("由于取消启动，已强制结束自定义命令 CMD 进程"); // #1183

                process.Kill();

                // 等待进程真正退出
                var timeout = TimeSpan.FromSeconds(5);
                var waitTask = Task.Run(() => {
                    try {
                        process.WaitForExit((int)timeout.TotalMilliseconds);
                    } catch (Exception ex) {
                        LogWrapper.Warn(ex, "Process termination wait failed");
                    }
                });

                await waitTask;
            }
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "Failed to terminate process safely");
        }
    }

    /// <summary>
    /// 自定义命令信息记录
    /// </summary>
    private readonly record struct CustomCommandInfo(string? Global, string? Version);
}
