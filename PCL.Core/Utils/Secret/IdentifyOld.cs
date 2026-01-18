using System;
using System.Management;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Utils.Hash;

namespace PCL.Core.Utils.Secret;

public static class IdentifyOld
{
    private const string DefaultRawCode = "B09675A9351CBD1FD568056781FE3966DD936CC9B94E51AB5CF67EEB7E74C075";
    private static readonly Lazy<string?> _LazyCpuId = new(_GetCpuId);

    private static readonly Lazy<string> _LazyRawCode =
        new(() => CpuId is null ? DefaultRawCode : SHA256Provider.Instance.ComputeHash(CpuId).ToUpper());

    private static readonly Lazy<string> _LaunchId = new(_GetLaunchId);

    private static readonly Lazy<string> _LazyEncryptKey =
        new(() => SHA512Provider.Instance.ComputeHash(RawCode).Substring(4, 32).ToUpper());

    public static string GetGuid() => Guid.NewGuid().ToString();
    [Obsolete]
    public static string? CpuId => _LazyCpuId.Value;
    [Obsolete]
    public static string RawCode => _LazyRawCode.Value;
    [Obsolete]
    public static string LaunchId => _LaunchId.Value;
    [Obsolete]
    public static string EncryptKey => _LazyEncryptKey.Value;

    private static string? _GetCpuId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            using var collection = searcher.Get();

            foreach (var item in collection)
            {
                try
                {
                    return item["ProcessorId"]?.ToString();
                }
                catch (ManagementException ex)
                {
                    LogWrapper.Warn("Identify", $"WMI属性读取失败: {ex.Message}");
                }
                finally
                {
                    item.Dispose();
                }
            }

            LogWrapper.Warn("Identify", "未找到有效的CPU ID");
            return null;
        }
        catch (ManagementException ex)
        {
            LogWrapper.Error(ex, "Identify", $"WMI查询失败");
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            LogWrapper.Error(ex, "Identify", $"COM异常，请确保WMI服务正在运行");
        }
        catch (UnauthorizedAccessException ex)
        {
            LogWrapper.Error(ex, "Identify", "访问被拒绝，请以管理员权限运行");
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Identify", $"意外的系统异常");
        }

        return null;
    }

    public static string GetMachineId(string randomId)
    {
        return SHA512Provider.Instance.ComputeHash($"{randomId}|{CpuId}").ToUpper();
    }

    private static string _GetLaunchId()
    {
        try
        {
            if (string.IsNullOrEmpty(Config.System.LaunchUuid)) Config.System.LaunchUuid = GetGuid();
            var hashCode = GetMachineId(Config.System.LaunchUuid)
                .Substring(6, 16)
                .Insert(4, "-")
                .Insert(9, "-")
                .Insert(14, "-");
            return hashCode;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Identify", "无法获取短识别码");
            return "PCL2-CECE-GOOD-2025";
        }
    }
}