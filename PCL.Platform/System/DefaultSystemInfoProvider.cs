// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using System.Globalization;
using PCL.Platform.Abstractions.System;

namespace PCL.Platform.System;

public sealed class DefaultSystemInfoProvider : ISystemInfoProvider
{
    public OperatingSystemInfo GetOperatingSystem() => new(
        RuntimeInformation.OSDescription,
        Environment.OSVersion.VersionString,
        RuntimeInformation.OSArchitecture.ToString(),
        Environment.Is64BitOperatingSystem);

    public MemoryInfo GetMemoryInfo()
    {
        if (OperatingSystem.IsWindows() && TryGetWindowsMemory(out MemoryInfo windowsMemory))
            return windowsMemory;
        if (OperatingSystem.IsLinux() && TryGetLinuxMemory(out MemoryInfo linuxMemory))
            return linuxMemory;

        GCMemoryInfo gcMemoryInfo = GC.GetGCMemoryInfo();
        long totalBytes = Math.Max(0, gcMemoryInfo.TotalAvailableMemoryBytes);
        long availableBytes = gcMemoryInfo.MemoryLoadBytes > 0 && totalBytes >= gcMemoryInfo.MemoryLoadBytes
            ? totalBytes - gcMemoryInfo.MemoryLoadBytes
            : 0;
        return new MemoryInfo(totalBytes, availableBytes);
    }

    public CpuInfo GetCpuInfo() => new(
        RuntimeInformation.ProcessArchitecture.ToString(),
        Environment.ProcessorCount,
        RuntimeInformation.ProcessArchitecture.ToString());

    private static bool TryGetWindowsMemory(out MemoryInfo memory)
    {
        MemoryStatusEx status = new()
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };
        if (GlobalMemoryStatusEx(ref status) && status.TotalPhysical > 0)
        {
            memory = new MemoryInfo(
                checked((long)Math.Min(status.TotalPhysical, long.MaxValue)),
                checked((long)Math.Min(status.AvailablePhysical, long.MaxValue)));
            return true;
        }

        memory = new MemoryInfo(0, 0);
        return false;
    }

    private static bool TryGetLinuxMemory(out MemoryInfo memory)
    {
        long totalKilobytes = 0;
        long availableKilobytes = 0;
        try
        {
            foreach (string line in File.ReadLines("/proc/meminfo"))
            {
                int separator = line.IndexOf(':');
                if (separator <= 0)
                    continue;

                string key = line[..separator];
                string valueText = line[(separator + 1)..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                if (!long.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
                    continue;

                if (key == "MemTotal")
                    totalKilobytes = value;
                else if (key == "MemAvailable")
                    availableKilobytes = value;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        if (totalKilobytes > 0 && availableKilobytes >= 0)
        {
            memory = new MemoryInfo(totalKilobytes * 1024, Math.Min(availableKilobytes, totalKilobytes) * 1024);
            return true;
        }

        memory = new MemoryInfo(0, 0);
        return false;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
