using PCL.Core.App.IoC;
using PCL.Core.IO;
using PCL.Core.Utils.OS;
using System;
using System.Threading;
using PCL.Core.App.Essentials;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI;

namespace PCL.Core.App.Tools;

[LifecycleService(LifecycleState.Running)]
[LifecycleScope("mem-swap", "内存交换", false)]
public sealed partial class MemSwapService
{
    private static readonly SemaphoreSlim _MemSwapLock = new(1, 1);

    /// <summary>
    /// 开始内存交换处理。
    /// </summary>
    /// <param name="showHint">是否显示提示</param>
    /// <returns>请求是否成功，注意：该结果不能表示在提权进程中执行的内存操作是否成功</returns>
    public static bool MemorySwap(bool showHint = true)
    {
        if (!_MemSwapLock.Wait(0))
        {
            Context.Warn("检测到正在进行的内存处理，取消当前处理");
            if (showHint) HintWrapper.Show(Lang.Text("Tools.Test.Memory.StillRunning"));
            return false;
        }
        Context.Info("收到内存交换请求，开始处理");
        if (showHint) HintWrapper.Show(Lang.Text("Tools.Test.Memory.Optimizing"));

        try
        {
            var before = KernelInterop.GetAvailablePhysicalMemoryBytes();
            Context.Info($"处理前内存量 {ByteStream.GetReadableLength((long)before)}");

            // 添加内存处理提权操作
            PromoteService.Append("mem-swap", result =>
            {
                try
                {
                    if (result is null)
                    {
                        var after = KernelInterop.GetAvailablePhysicalMemoryBytes();
                        Context.Info($"处理后内存量 {ByteStream.GetReadableLength((long)after)}");
                        var diff = Math.Max(0, after - before);
                        var afterStr = ByteStream.GetReadableLength((long)after);
                        var diffStr = ByteStream.GetReadableLength((long)diff);
                        Context.Info($"处理结束，总共处理 {diffStr}");
                        if (showHint) MsgBoxWrapper.Show(Lang.Text("Tools.Test.Memory.Result", diffStr, afterStr));
                    }
                    else
                    {
                        Context.Error(Lang.Text("Tools.Test.Memory.FailedMessage", result), actionLevel: ActionLevel.MsgBoxErr);
                    }
                }
                catch (Exception ex) { Context.Error("内存优化失败", ex); }
                finally { _MemSwapLock.Release(); }
            });

            // 执行操作
            if (PromoteService.Activate()) return true;
            _MemSwapLock.Release();
            if (showHint) MsgBoxWrapper.Show(Lang.Text("Tools.Test.Memory.PromoteFailed"), Lang.Text("Tools.Test.Memory.Failed"));
            return false;
        }
        catch (Exception)
        {
            _MemSwapLock.Release();
            throw;
        }
    }

    [LifecycleCommandHandler("memory")]
    private static void _MemSwapTriggered(bool isCallback) => MemorySwap(isCallback);

    private static bool _privilegesAcquired = false;

    [PromoteOperation("mem-swap")]
    public static string? OnPromoteMemorySwapOperation(string? arg)
    {
        var scope = Enum.Parse<MemSwapScope>(arg ?? nameof(MemSwapScope.All));
        if (!_privilegesAcquired)
        {
            _AcquirePrivileges();
            _privilegesAcquired = true;
        }
        _OnMemorySwap(scope);
        return null;
    }

    private static void _AcquirePrivileges()
    {
        LogWrapper.Info("MemSwap", "获取内存管理权限……");
        NtInterop.SetPrivilege(NtInterop.SePrivilege.SeProfileSingleProcessPrivilege, true, false);
        NtInterop.SetPrivilege(NtInterop.SePrivilege.SeIncreaseQuotaPrivilege, true, false);
    }

    private static void _OnMemorySwap(MemSwapScope scope = MemSwapScope.All)
    {
        LogWrapper.Info("MemSwap", $"开始处理，区域请求：{scope} ({(int)scope})");
        if (scope.HasFlag(MemSwapScope.EmptyWorkingSets)) MemSwapWorks.EmptyWorkingSets();
        if (scope.HasFlag(MemSwapScope.FlushFileCache)) MemSwapWorks.FlushFileCache();
        if (scope.HasFlag(MemSwapScope.FlushModifiedList)) MemSwapWorks.FlushModifiedList();
        if (scope.HasFlag(MemSwapScope.PurgeStandbyList)) MemSwapWorks.PurgeStandbyList();
        if (scope.HasFlag(MemSwapScope.PurgeLowPriorityStandbyList)) MemSwapWorks.PurgeLowPriorityStandbyList();
        if (scope.HasFlag(MemSwapScope.RegistryReconciliation)) MemSwapWorks.RegistryReconciliation();
        if (scope.HasFlag(MemSwapScope.CombinePhysicalMemory)) MemSwapWorks.CombinePhysicalMemory();
    }
}
