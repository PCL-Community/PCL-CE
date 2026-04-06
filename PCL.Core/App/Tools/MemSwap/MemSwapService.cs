using PCL.Core.App.IoC;
using PCL.Core.IO;
using PCL.Core.Utils.OS;
using System;
using System.Threading;

namespace PCL.Core.App.Tools.MemSwap;

[LifecycleService(LifecycleState.BeforeLoading, Priority = 128)]
[LifecycleScope("mem-swap", "内存交换", false)]
public sealed partial class MemSwapService
{
    [LifecycleStart]
    private static void _CheckRequest()
    {
        var args = Basics.CommandLineArguments;
        if (args is not ["memory"]) return;

        Context.Info("检测到内存交换请求，开始处理");

        if (!ProcessInterop.IsAdmin())
        {
            Context.Error("缺少管理员权限，退出内存处理");
            Context.RequestExit(-1);
            return;
        }

        try
        {
            var before = KernelInterop.GetPhysicalMemoryBytes().Available;
            Context.Info($"处理前内存量 {ByteStream.GetReadableLength((long)before)}");
            AcquirePrivileges();
            if (!MemorySwap(SwapScope.All))
            {
                Context.Error("请求无法处理，返回");
                Context.RequestExit(-1);
                return;
            }

            var after = KernelInterop.GetPhysicalMemoryBytes().Available;
            Context.Info($"处理后内存量 {ByteStream.GetReadableLength((long)after)}");
            var diff = Math.Max(0, after - before);
            Context.Info($"处理结束，总共处理 {ByteStream.GetReadableLength((long)diff)}");
            diff /= 1024;
            if (diff > int.MaxValue) diff = int.MaxValue;

            Context.RequestExit((int)diff);
        }
        catch (Exception ex)
        {
            Context.Error("内存处理失败", ex);
            Context.RequestExit(-1);
        }
    }

    private static readonly SemaphoreSlim _MemSwapLock = new(1, 1);
    public static bool MemorySwap(SwapScope scope = SwapScope.All)
    {
        if (!_MemSwapLock.Wait(0))
        {
            Context.Warn("检测到正在进行的内存处理，取消当前处理");
            return false;
        }

        try
        {
            if (!ProcessInterop.IsAdmin()) return false;

            Context.Info($"开始处理，区域请求：{(int)scope}");
            if (scope.HasFlag(SwapScope.EmptyWorkingSets)) SwapWorks.EmptyWorkingSets();
            if (scope.HasFlag(SwapScope.FlushFileCache)) SwapWorks.FlushFileCache();
            if (scope.HasFlag(SwapScope.FlushModifiedList)) SwapWorks.FlushModifiedList();
            if (scope.HasFlag(SwapScope.PurgeStandbyList)) SwapWorks.PurgeStandbyList();
            if (scope.HasFlag(SwapScope.PurgeLowPriorityStandbyList)) SwapWorks.PurgeLowPriorityStandbyList();
            if (scope.HasFlag(SwapScope.RegistryReconciliation)) SwapWorks.RegistryReconciliation();
            if (scope.HasFlag(SwapScope.CombinePhysicalMemory)) SwapWorks.CombinePhysicalMemory();

            return true;
        }
        catch (Exception ex)
        {
            Context.Error("内存处理出现异常", ex);
            return false;
        }
        finally
        {
            _MemSwapLock.Release();
        }
    }

    public static void AcquirePrivileges()
    {
        Context.Info("获取权限……");
        NtInterop.SetPrivilege(NtInterop.SePrivilege.SeProfileSingleProcessPrivilege, true, false);
        NtInterop.SetPrivilege(NtInterop.SePrivilege.SeIncreaseQuotaPrivilege, true, false);
    }
}
