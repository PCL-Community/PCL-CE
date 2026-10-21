using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using PCL.Core.App;

namespace PCL.Core.Utils.OS;

/// <summary>
/// 系统内存优化，对应原版 PCL 百宝箱中的“内存优化”功能。<br/>
/// 通过 <c>NtSetSystemInformation(SystemMemoryListInformation)</c> 清空系统内存列表
/// （已修改页列表、备用列表、低优先级备用列表），需要 <c>SeProfileSingleProcessPrivilege</c> 权限与管理员身份。
/// </summary>
public static partial class MemoryOptimizer
{
    /// <summary>
    /// 指示当前是否正在执行内存优化。
    /// </summary>
    public static bool IsOptimizing
    {
        get
        {
            lock (_Lock) return _isOptimizing;
        }
    }

    private static bool _isOptimizing;
    private static readonly object _Lock = new();

    /// <summary>
    /// 执行内存优化，并返回优化前后的可用物理内存变化量（字节，可能为负）。<br/>
    /// 若当前进程没有管理员权限，会自动以管理员身份启动辅助进程（--memory）执行优化，
    /// 其退出码为清理的内存量（KB），对应原版 PCL 的行为。
    /// </summary>
    /// <exception cref="InvalidOperationException">上一次内存优化尚未结束时抛出</exception>
    /// <exception cref="UnauthorizedAccessException">无法获取管理员权限时抛出</exception>
    /// <exception cref="Win32Exception">内存优化过程中出现错误时抛出</exception>
    public static long Optimize()
    {
        lock (_Lock)
        {
            if (_isOptimizing) throw new InvalidOperationException("上一次内存优化尚未结束");
            _isOptimizing = true;
        }
        try
        {
            if (!ProcessInterop.IsAdmin())
            {
                const string errorMessage = "获取管理员权限失败，请尝试右键 PCL CE，选择“以管理员身份运行”！";
                Process? process;
                try
                {
                    process = ProcessInterop.StartAsAdmin(Basics.ExecutablePath, "--memory");
                }
                catch (Exception ex)
                {
                    throw new UnauthorizedAccessException(errorMessage, ex);
                }
                if (process is null) throw new UnauthorizedAccessException(errorMessage);
                using (process)
                {
                    process.WaitForExit();
                    var freedKb = process.ExitCode;
                    if (freedKb < 0) throw new UnauthorizedAccessException(errorMessage);
                    return freedKb * 1024L;
                }
            }
            var oldMemory = KernelInterop.GetAvailablePhysicalMemoryBytes();
            OptimizeCore();
            var newMemory = KernelInterop.GetAvailablePhysicalMemoryBytes();
            return (long)(newMemory - oldMemory);
        }
        finally
        {
            lock (_Lock) _isOptimizing = false;
        }
    }

    /// <summary>
    /// 执行内存优化的核心操作：申请 <c>SeProfileSingleProcessPrivilege</c> 权限并清空系统内存列表。
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">没有管理员权限时抛出</exception>
    /// <exception cref="Win32Exception">申请权限或清空内存列表失败时抛出</exception>
    public static void OptimizeCore()
    {
        if (!ProcessInterop.IsAdmin())
            throw new UnauthorizedAccessException("内存优化功能需要管理员权限！");
        _EnablePrivilege("SeProfileSingleProcessPrivilege");
        for (var typeId = 2; typeId <= 4; typeId++)
        {
            var handle = GCHandle.Alloc(typeId, GCHandleType.Pinned);
            try
            {
                var result = _NtSetSystemInformation(SystemMemoryListInformation, handle.AddrOfPinnedObject(), sizeof(int));
                if (result != 0)
                    throw new Win32Exception($"内存优化操作 {typeId} 失败（错误代码：{result}）");
            }
            finally
            {
                handle.Free();
            }
        }
    }

    /// <summary>
    /// 为当前进程令牌启用指定特权。
    /// </summary>
    private static void _EnablePrivilege(string privilegeName)
    {
        using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.AdjustPrivileges);
        var newState = new PrivilegeToken { PrivilegeCount = 1, Luid = 0, Attributes = SePrivilegeEnabled };
        if (!_LookupPrivilegeValue(null, privilegeName, ref newState.Luid))
            throw new Win32Exception($"获取内存优化权限失败（错误代码：{Marshal.GetLastWin32Error()}）");
        var previousState = IntPtr.Zero;
        var returnLength = 0;
        if (!_AdjustTokenPrivileges(identity.Token, false, ref newState, Marshal.SizeOf<PrivilegeToken>(),
                previousState, ref returnLength) ||
            Marshal.GetLastWin32Error() != 0)
            throw new Win32Exception($"获取内存优化权限失败（错误代码：{Marshal.GetLastWin32Error()}）");
    }

    private const int SePrivilegeEnabled = 2;
    private const int SystemMemoryListInformation = 80;

    /// <summary>
    /// 扁平化的 <c>TOKEN_PRIVILEGES</c> 结构（仅包含一个特权）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct PrivilegeToken
    {
        public int PrivilegeCount;
        public long Luid;
        public int Attributes;
    }

    [LibraryImport("ntdll.dll", EntryPoint = "NtSetSystemInformation")]
    private static partial int _NtSetSystemInformation(int systemInformationClass, IntPtr systemInformation,
        int systemInformationLength);

    [LibraryImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueA", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool _LookupPrivilegeValue(
        [MarshalAs(UnmanagedType.LPStr)] string? lpSystemName,
        [MarshalAs(UnmanagedType.LPStr)] string lpName,
        ref long lpLuid);

    [LibraryImport("advapi32.dll", EntryPoint = "AdjustTokenPrivileges", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool _AdjustTokenPrivileges(IntPtr tokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges, ref PrivilegeToken newState, int bufferLength,
        IntPtr previousState, ref int returnLength);
}
