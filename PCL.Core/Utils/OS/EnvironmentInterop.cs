using System;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Utils.OS;

public static partial class EnvironmentInterop
{
    private const string LogModule = "Environment";

    /// <summary>
    /// 读取环境变量并使用 <see cref="StringExtension.Convert{T}"/> 将其转换为指定类型并写入目标引用。
    /// </summary>
    /// <param name="key">环境变量名</param>
    /// <param name="target">需要写入的目标引用 (不存在该环境变量或转换失败时不会写入)</param>
    /// <param name="detailLog">是否在日志中输出变量值</param>
    /// <typeparam name="TValue">目标引用的类型</typeparam>
    /// <returns>是否成功写入目标引用</returns>
    public static bool ReadVariable<TValue>(string key, ref TValue target, bool detailLog = true)
    {
        var envValue = Environment.GetEnvironmentVariable(key);
        if (envValue == null) return false;
        var valueLog = detailLog ? $" = {envValue}" : string.Empty;
        LogWrapper.Debug(LogModule, $"读取到环境变量 {key}{valueLog}");
        var value = envValue.Convert<TValue>();
        if (value == null)
        {
            LogWrapper.Warn(LogModule, $"环境变量 {key} 类型转换失败");
            return false;
        }
        target = value;
        return true;
    }

    public static string? GetSecret(string key, bool readEnv = true, bool readEnvDebugOnly = false)
    {
        if (!SecretDictionary.TryGetValue(key, out var value) &&
            readEnv &&
#if !DEBUG
            !readEnvDebugOnly &&
#endif
            ReadVariable($"PCL_{key}", ref value, false)
        ) SecretDictionary[key] = value;
        return value;
    }
    
    /// <summary>
    /// 获取当前操作系统名称。
    /// </summary>
    /// <returns>返回小写的操作系统名称，如 "windows", "linux", "osx"。</returns>
    public static string GetCurrentOsName() {
        if (OperatingSystem.IsWindows())
            return "windows";
        if (OperatingSystem.IsLinux())
            return "linux";
        return OperatingSystem.IsMacOS() 
            ? "osx"
            : "unknown";
    }
}
