using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Security;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Win32;
using PCL.Core.Logging;

namespace PCL.Core.Utils.OS;

public class ProcessInterop {
    /// <summary>
    /// 检查当前程序是否以管理员权限运行。
    /// </summary>
    /// <returns>如果当前用户具有管理员权限，则返回 true；否则返回 false。</returns>
    public static bool IsAdmin() =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>
    /// 获取指定进程 ID 的命令行参数。
    /// </summary>
    /// <param name="processId">进程 ID</param>
    /// <returns>命令行参数文本</returns>
    public static string? GetCommandLine(int processId) {
        var query = $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}";
        using var searcher = new ManagementObjectSearcher(query);
        return searcher.Get().GetEnumerator().Current["CommandLine"].ToString();
    }

    /// <summary>
    /// 从本地可执行文件启动新的进程。
    /// </summary>
    /// <param name="path">可执行文件路径</param>
    /// <param name="arguments">程序参数</param>
    /// <param name="runAsAdmin">指定是否以管理员身份启动该进程</param>
    /// <returns>新的进程实例</returns>
    public static Process? Start(string path, string? arguments = null, bool runAsAdmin = false) {
        var psi = new ProcessStartInfo(path);
        if (arguments != null) psi.Arguments = arguments;
        if (runAsAdmin) psi.Verb = "runas";
        return Process.Start(psi);
    }

    /// <summary>
    /// 获取指定进程的可执行文件路径
    /// </summary>
    /// <param name="process">进程实例</param>
    /// <returns>可执行文件路径，若无法获取则为 <c>null</c></returns>
    public static string? GetExecutablePath(Process process) {
        try {
            var path = process.MainModule?.FileName;
            return (path == null) ? null : Path.GetFullPath(path);
        } catch { return null; }
    }

    /// <summary>
    /// 从本地可执行文件以管理员身份启动新的进程。<see cref="Start"/> 的套壳。
    /// </summary>
    /// <param name="path">可执行文件路径</param>
    /// <param name="arguments">程序参数</param>
    /// <returns>新的进程实例</returns>
    public static Process? StartAsAdmin(string path, string? arguments = null) => Start(path, arguments, true);

    /// <summary>
    /// 结束指定进程。
    /// </summary>
    /// <param name="process">要结束的进程实例</param>
    /// <param name="timeout">等待进程退出超时，以毫秒为单位，-1 表示无限制</param>
    /// <param name="force">指定是否强制结束，若为 <c>true</c> 将通过带 <c>/F</c> 参数的 <c>TASKKILL.EXE</c> 结束进程</param>
    /// <returns>进程返回值，若等待超时将返回 <see cref="int.MinValue"/></returns>
    public static int Kill(Process process, int timeout = 3000, bool force = false) {
        if (force) Process.Start(new ProcessStartInfo("TASKKILL.EXE", $"/PID {process.Id} /F") { UseShellExecute = false });
        else process.Kill();
        if (timeout == -1) process.WaitForExit();
        else if (timeout != 0) process.WaitForExit(timeout);
        return process.HasExited ? process.ExitCode : int.MinValue;
    }

    /// <summary>
    /// 将特定程序设置为使用高性能显卡启动。
    /// </summary>
    /// <param name="executable">可执行文件路径。</param>
    /// <param name="wantHighPerformance">是否使用高性能显卡，默认为 true。</param>
    /// <exception cref="ArgumentException">当可执行文件路径无效时抛出</exception>
    /// <exception cref="UnauthorizedAccessException">当没有足够权限访问注册表时抛出</exception>
    /// <exception cref="SecurityException">当安全策略不允许访问注册表时抛出</exception>
    /// <exception cref="InvalidOperationException">当注册表操作失败时抛出</exception>
    public static void SetGpuPreference(string executable, bool wantHighPerformance = true) {
        // 参数验证
        if (string.IsNullOrWhiteSpace(executable)) {
            throw new ArgumentException("可执行文件路径不能为空或仅包含空白字符", nameof(executable));
        }

        // 验证文件路径格式
        try {
            var fullPath = Path.GetFullPath(executable);
            if (!File.Exists(fullPath)) {
                LogWrapper.Warn("System", $"指定的可执行文件不存在: {executable}");
            }
        } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) {
            throw new ArgumentException($"无效的可执行文件路径: {executable}", nameof(executable), ex);
        }

        const string gpuPreferenceRegKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
        const string gpuPreferenceRegValueHigh = "GpuPreference=2;";
        const string gpuPreferenceRegValueDefault = "GpuPreference=0;";

        try {
            var isCurrentHighPerformance = GetCurrentGpuPreference(executable, gpuPreferenceRegKey, gpuPreferenceRegValueHigh);

            LogWrapper.Info("System", $"当前程序 ({executable}) 的显卡设置为高性能: {isCurrentHighPerformance}");

            // 如果当前设置已经是期望的设置，则无需修改
            if (isCurrentHighPerformance == wantHighPerformance) {
                LogWrapper.Info("System", $"程序 ({executable}) 的显卡设置已经是期望的设置，无需修改");
                return;
            }

            // 写入新设置
            SetGpuPreferenceValue(executable, wantHighPerformance, gpuPreferenceRegKey,
                gpuPreferenceRegValueHigh, gpuPreferenceRegValueDefault);
        } catch (UnauthorizedAccessException ex) {
            var errorMsg = "没有足够的权限访问注册表。请以管理员身份运行程序或检查用户权限设置。";
            LogWrapper.Error(ex, "System", errorMsg);
            throw new UnauthorizedAccessException(errorMsg, ex);
        } catch (SecurityException ex) {
            var errorMsg = "安全策略不允许访问注册表。请联系系统管理员检查安全设置。";
            LogWrapper.Error(ex, "System", errorMsg);
            throw new SecurityException(errorMsg, ex);
        } catch (Exception ex) {
            var errorMsg = $"设置 GPU 偏好时发生未预期的错误: {ex.Message}";
            LogWrapper.Error(ex, "System", errorMsg);
            throw new InvalidOperationException(errorMsg, ex);
        }
    }

    /// <summary>
    /// 获取当前程序的GPU偏好设置
    /// </summary>
    private static bool GetCurrentGpuPreference(string executable, string regKey, string highPerfValue) {
        try {
            using var readOnlyKey = Registry.CurrentUser.OpenSubKey(regKey, false);
            if (readOnlyKey == null) {
                LogWrapper.Info("System", "GPU 偏好注册表键不存在，将在需要时创建");
                return false;
            }

            var currentValue = readOnlyKey.GetValue(executable)?.ToString();
            return string.Equals(currentValue, highPerfValue, StringComparison.OrdinalIgnoreCase);
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "System", $"读取当前 GPU 偏好设置时出现错误: {ex.Message}");
            return false; // 假设当前不是高性能模式
        }
    }

    /// <summary>
    /// 设置GPU偏好值到注册表
    /// </summary>
    private static bool SetGpuPreferenceValue(string executable, bool wantHighPerformance,
        string regKey, string highPerfValue, string defaultValue) {
        RegistryKey? writeKey = null;
        try {
            // 尝试打开现有键进行写入
            writeKey = Registry.CurrentUser.OpenSubKey(regKey, true);

            // 如果键不存在，创建它
            if (writeKey == null) {
                LogWrapper.Info("System", "创建 GPU 偏好注册表键");
                writeKey = Registry.CurrentUser.CreateSubKey(regKey);

                if (writeKey == null) {
                    throw new InvalidOperationException($"无法创建注册表键: {regKey}");
                }
            }

            var valueToSet = wantHighPerformance ? highPerfValue : defaultValue;
            writeKey.SetValue(executable, valueToSet, RegistryValueKind.String);

            LogWrapper.Info("System", $"成功设置程序 ({executable}) 的GPU偏好: {(wantHighPerformance ? "高性能" : "默认")}");
            return true;
        } catch (UnauthorizedAccessException) {
            // 重新抛出，让上层处理
            throw;
        } catch (SecurityException) {
            // 重新抛出，让上层处理
            throw;
        } catch (Exception ex) {
            var errorMsg = $"写入注册表时发生错误: {ex.Message}";
            LogWrapper.Error(ex, "System", errorMsg);
            throw new InvalidOperationException(errorMsg, ex);
        } finally {
            writeKey?.Dispose();
        }
    }
}

public enum ProcessExitCode {
    /// <summary>
    /// Indicates that the process completed successfully.
    /// </summary>
    TaskDone = 0,

    /// <summary>
    /// Indicates a general failure of the process.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// Indicates the process was canceled.
    /// </summary>
    Canceled = 2,

    /// <summary>
    /// Indicates the process failed due to insufficient permissions.
    /// </summary>
    AccessDenied = 5
}
