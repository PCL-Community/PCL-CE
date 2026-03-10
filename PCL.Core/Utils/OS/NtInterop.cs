using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PCL.Core.Utils.OS;

public static partial class NtInterop
{
    [LibraryImport("ntdll.dll")]
    private static partial void RtlGetNtVersionNumbers(
        out int major,
        out int minor,
        out int build);
    
    [LibraryImport("ntdll.dll")]
    private static partial uint RtlAdjustPrivilege(
        SePrivilege privilege,
        [MarshalAs(UnmanagedType.U1)] bool enable,
        [MarshalAs(UnmanagedType.U1)] bool currentThread,
        [MarshalAs(UnmanagedType.U1)] out bool enabled);
    
    [LibraryImport("ntdll.dll")]
    private static partial ulong RtlNtStatusToDosError(uint status);
    
    [LibraryImport("ntdll.dll")]
    private static partial uint NtSetSystemInformation(
        SystemInformationClass systemInformationClass,
        IntPtr systemInformation,
        uint systemInformationLength);

    public enum SePrivilege : uint
    {
        SeIncreaseQuotaPrivilege = 5,
        SeProfileSingleProcessPrivilege = 13
    }

    public enum SystemInformationClass
    {
        SystemMemoryListInformation = 80,
        SystemFileCacheInformationEx = 81,
        SystemCombinePhysicalMemoryInformation = 130,
        SystemRegistryReconciliationInformation = 155
    }
    
    private static void _ThrowLastWin32Error(int? errorCode = null) => throw new Win32Exception(errorCode ?? Marshal.GetLastWin32Error());
    
    /// <summary>
    /// Retrieve the kernel version number of the current operating system (unaffected by compatibility settings)
    /// </summary>
    /// <returns>A <see cref="Version"/> instance, used to represent the current operating system kernel version number.</returns>
    public static Version GetCurrentOsVersion()
    {
        RtlGetNtVersionNumbers(out var major, out var minor, out var build);
        build &= 0xFFFF;
        return new Version(major, minor, build);
    }
    
    /// <summary>
    /// 设置当前进程或线程的相应特权。
    /// </summary>
    /// <param name="privilege">要获取的特权。</param>
    /// <param name="state">控制特权的开关。</param>
    /// <param name="currentThread">是否为当前线程设置特权。</param>
    /// <returns>返回原来相应特权的状态。</returns>
    public static bool SetPrivilege(SePrivilege privilege, bool state, bool currentThread = true)
    {
        var result = RtlAdjustPrivilege(privilege, state, currentThread, out var enabled);
        if (result != 0) _ThrowLastWin32Error((int)RtlNtStatusToDosError(result));
        return enabled;
    }
    
    public static void SetSystemInformation(SystemInformationClass infoClass, IntPtr info, uint infoLength)
    {
        var result = NtSetSystemInformation(infoClass, info, infoLength);
        if (result != 0) _ThrowLastWin32Error((int)RtlNtStatusToDosError(result));
    }
}
