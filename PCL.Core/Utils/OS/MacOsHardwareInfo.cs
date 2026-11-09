using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.Utils.OS;

/// <summary>macOS 硬件信息采集实现。</summary>
public sealed class MacOsHardwareInfo : IHardwareInfo
{
    public string? GetCpuName()
    {
        var cpuName = _RunAndRead("sysctl", "-n machdep.cpu.brand_string").Trim();
        if (string.IsNullOrWhiteSpace(cpuName) || cpuName == "0")
            cpuName = _RunAndRead("sysctl", "-n hw.model").Trim();
        if (string.IsNullOrWhiteSpace(cpuName))
            cpuName = _ReadMacHardwareName();

        return string.IsNullOrWhiteSpace(cpuName) ? null : cpuName;
    }

    public IReadOnlyList<HardwareInfo.GpuInfo> GetGpus()
    {
        return _ReadMacOsGpus();
    }

    public long GetMemorySizeMiB()
    {
        return (long)KernelInterop.GetPhysicalMemoryBytes().Total / 1024 / 1024;
    }

    private static string _ReadMacHardwareName()
    {
        var output = _RunAndRead("system_profiler", "SPHardwareDataType");
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Chip:", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Processor Name:", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Model Name:", StringComparison.OrdinalIgnoreCase))
                return _GetCpuInfoValue(trimmed);
        }

        return string.Empty;
    }

    private static List<HardwareInfo.GpuInfo> _ReadMacOsGpus()
    {
        var gpuList = new List<HardwareInfo.GpuInfo>();
        var output = _RunAndRead("system_profiler", "SPDisplaysDataType");
        string? name = null;
        var driver = string.Empty;
        long memory = 0;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Chipset Model:", StringComparison.OrdinalIgnoreCase))
            {
                AddCurrentGpu();
                name = _GetCpuInfoValue(trimmed);
            }
            else if (trimmed.StartsWith("Metal Support:", StringComparison.OrdinalIgnoreCase))
            {
                driver = _GetCpuInfoValue(trimmed);
            }
            else if (trimmed.StartsWith("VRAM (Total):", StringComparison.OrdinalIgnoreCase)
                     || trimmed.StartsWith(
                         "VRAM (Dynamic, Max):",
                         StringComparison.OrdinalIgnoreCase))
            {
                memory = _ParseMacVRamMiB(_GetCpuInfoValue(trimmed));
            }
        }

        AddCurrentGpu();
        return gpuList;

        void AddCurrentGpu()
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            gpuList.Add(new HardwareInfo.GpuInfo(name, driver, memory));
            name = null;
            driver = string.Empty;
            memory = 0;
        }
    }

    private static long _ParseMacVRamMiB(string value)
    {
        var text = value.Trim();
        var numericChars = text
            .TakeWhile(c => char.IsDigit(c) || c is '.' or ',')
            .ToArray();
        if (!double.TryParse(
                new string(numericChars).Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var amount))
            return 0;

        if (text.Contains("GB", StringComparison.OrdinalIgnoreCase))
            amount *= 1024d;

        return (long)Math.Round(amount);
    }

    private static string _RunAndRead(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(
                new ProcessStartInfo(fileName, arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Environment =
                    {
                        ["LANG"] = "C"
                    }
                });
            if (process is null) return string.Empty;

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // 进程已退出
                }

                return string.Empty;
            }

            _ = errorTask.GetAwaiter().GetResult();
            return outputTask.GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
        {
            LogWrapper.Debug("Hardware", $"执行 {fileName} {arguments} 失败：{ex.Message}");
            return string.Empty;
        }
    }

    private static string _GetCpuInfoValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        return colonIndex < 0 ? string.Empty : line[(colonIndex + 1)..].Trim();
    }
}