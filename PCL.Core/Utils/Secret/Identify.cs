using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Hash;

namespace PCL.Core.Utils.Secret;

public class Identify
{
    public static byte[] RawId
    {
        get => field ??= _GetRawId();
    } = null!;

    public static string LauncherId
    {
        get => field ??= _getLauncherId();
    } = null!;

    private static byte[] _GetRawId()
    {
        var code = new StringBuilder();
        if (OperatingSystem.IsWindows())
            try
            {
                code
                    .Append("UUID:")
                    .Append(_GetWmiProperty("Win32_ComputerSystemProduct", "UUID"))
                    .Append("|MB_Prod:")
                    .Append(_GetWmiProperty("Win32_BaseBoard", "Product"))
                    .Append("|MB_SN:")
                    .Append(_GetWmiProperty("Win32_BaseBoard", "SerialNumber"))
                    .Append("|CPU:")
                    .Append(_GetWmiProperty("Win32_Processor", "ProcessorId"));
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Identify", "获取设备基础信息失败");
            }
        else
            code.Append(GetNonWindowsFingerprintMaterial());

        return Encoding.UTF8.GetBytes(
            SHA512Provider.Instance.ComputeHash(code.ToString()).ToHexString());
    }

    /// <summary>
    ///     非 Windows 设备指纹材料：优先 machine-id 与 DMI 信息，全部不可读时退回主机名与架构。
    ///     部分 DMI 文件仅 root 可读，缺失时跳过即可，仍可得到稳定指纹。
    /// </summary>
    internal static string GetNonWindowsFingerprintMaterial()
    {
        var parts = new List<string>();
        _AppendFingerprintPart(parts, "machine-id", "/etc/machine-id");
        _AppendFingerprintPart(parts, "product_uuid", "/sys/class/dmi/id/product_uuid");
        _AppendFingerprintPart(parts, "board_serial", "/sys/class/dmi/id/board_serial");
        _AppendFingerprintPart(parts, "product_serial", "/sys/class/dmi/id/product_serial");

        if (parts.Count != 0)
            return string.Join("|", parts);

        parts.Add($"host:{Environment.MachineName}");
        parts.Add($"arch:{RuntimeInformation.OSArchitecture}");

        return string.Join("|", parts);
    }

    private static void _AppendFingerprintPart(List<string> parts, string key, string path)
    {
        try
        {
            var value = File.ReadAllText(path).Trim();
            if (value.Length > 0) parts.Add($"{key}:{value}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 文件不存在或当前用户不可读时跳过该项
        }
    }

    [SupportedOSPlatform("windows")]
    private static string _GetWmiProperty(string className, string propertyName)
    {
        try
        {
            using var searcher =
                new ManagementObjectSearcher($"SELECT {propertyName} FROM {className}");
            using var results = searcher.Get();
            foreach (var obj in results)
            {
                if (obj[propertyName] is not null)
                    return (obj[propertyName].ToString() ?? string.Empty).Trim();
            }
        }
        catch
        {
            /* Ignore */
        }

        return string.Empty;
    }

    private static string _getLauncherId()
    {
        try
        {
            var prefix = "PCL-CE|"u8.ToArray();
            var ctx = RawId;
            var suffix = "|LauncherId"u8.ToArray();

            var buffer = new byte[prefix.Length + ctx.Length + suffix.Length];
            var bufferSpan = buffer.AsSpan();
            prefix.CopyTo(bufferSpan[..prefix.Length]);
            ctx.CopyTo(bufferSpan.Slice(prefix.Length, ctx.Length));
            suffix.CopyTo(bufferSpan.Slice(prefix.Length + ctx.Length, suffix.Length));

            Array.Clear(ctx);
            var sample = SHA512Provider.Instance.ComputeHash(bufferSpan).ToHexString();
            bufferSpan.Clear();

            // 16 in length, 8 bytes, 64 bits, enough for us
            return sample
                .Substring(64, 16)
                .ToUpper()
                .Insert(4, "-")
                .Insert(9, "-")
                .Insert(14, "-");
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Identify", "无法获取识别码");
            return "PCL2-CECE-GOOD-2025";
        }
    }
}