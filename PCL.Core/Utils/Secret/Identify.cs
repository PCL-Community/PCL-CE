using System;
using System.Management;
using System.Text;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Hash;

namespace PCL.Core.Utils.Secret;

public class Identify
{
    public static byte[] RawId { get; } = _GetRawId();
    public static string LauncherId { get; } = _getLauncherId();

    private static byte[] _GetRawId()
    {
        var code = new StringBuilder();
        try
        {
            code.Append("UUID:").Append(_GetWmiProperty("Win32_ComputerSystemProduct", "UUID"))
                .Append("|MB_Prod:").Append(_GetWmiProperty("Win32_BaseBoard", "Product"))
                .Append("|MB_SN:").Append(_GetWmiProperty("Win32_BaseBoard", "SerialNumber"))
                .Append("|CPU:").Append(_GetWmiProperty("Win32_Processor", "ProcessorId"));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Identify", "获取设备基础信息失败");
        }

        return Encoding.UTF8.GetBytes(SHA512Provider.Instance.ComputeHash(code.ToString()));
    }

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
        catch { /* Ignore */ }
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
            var sample = SHA512Provider.Instance.ComputeHash(bufferSpan);
            bufferSpan.Clear();

            // 16 in length, 8 bytes, 64 bits, enough for us
            return sample.Substring(64, 16)
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