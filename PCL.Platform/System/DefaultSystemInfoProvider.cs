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
        if (OperatingSystem.IsMacOS() && TryGetMacMemory(out MemoryInfo macMemory))
            return macMemory;

        GCMemoryInfo gcMemoryInfo = GC.GetGCMemoryInfo();
        long totalBytes = Math.Max(0, gcMemoryInfo.TotalAvailableMemoryBytes);
        long availableBytes = gcMemoryInfo.MemoryLoadBytes > 0 && totalBytes >= gcMemoryInfo.MemoryLoadBytes
            ? totalBytes - gcMemoryInfo.MemoryLoadBytes
            : Math.Max(0, totalBytes - GC.GetTotalMemory(forceFullCollection: false));
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

    private static bool TryGetMacMemory(out MemoryInfo memory)
    {
        if (!TryReadMacSysctl("hw.memsize", out ulong total) || total == 0)
        {
            memory = new MemoryInfo(0, 0);
            return false;
        }

        TryReadMacSysctl("hw.pagesize", out ulong pageSize);
        ulong availablePages = 0;
        foreach (string key in new[]
                 {
                     "vm.page_free_count",
                     "vm.page_inactive_count",
                     "vm.page_speculative_count",
                     "vm.page_purgeable_count"
                 })
        {
            if (TryReadMacSysctl(key, out ulong pages))
                availablePages = checked(availablePages + pages);
        }

        ulong available = pageSize > 0 && availablePages > 0
            ? Math.Min(total, checked(pageSize * availablePages))
            : Math.Min(total, (ulong)Math.Max(1L, checked((long)Math.Min(total, long.MaxValue)) - GC.GetTotalMemory(false)));
        memory = new MemoryInfo(
            checked((long)Math.Min(total, long.MaxValue)),
            checked((long)Math.Min(available, long.MaxValue)));
        return true;
    }

    private static bool TryReadMacSysctl(string name, out ulong value)
    {
        nuint length = sizeof(ulong);
        value = 0;
        if (SysctlByName(name, ref value, ref length, IntPtr.Zero, 0) == 0 && length == sizeof(ulong))
            return true;

        uint smallValue = 0;
        length = sizeof(uint);
        if (SysctlByName32(name, ref smallValue, ref length, IntPtr.Zero, 0) != 0 || length != sizeof(uint))
            return false;

        value = smallValue;
        return true;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("libSystem.dylib", EntryPoint = "sysctlbyname", SetLastError = true, CharSet = CharSet.Ansi,
        BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern int SysctlByName(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        ref ulong oldValue,
        ref nuint oldLength,
        IntPtr newValue,
        nuint newLength);

    [DllImport("libSystem.dylib", EntryPoint = "sysctlbyname", SetLastError = true, CharSet = CharSet.Ansi,
        BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern int SysctlByName32(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        ref uint oldValue,
        ref nuint oldLength,
        IntPtr newValue,
        nuint newLength);

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
