using PCL.Core.App.IoC;
using PCL.Core.IO;
using PCL.Core.Utils.OS;
using System;
using System.Threading;
using PCL.Core.App.Essentials;
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
    public static void MemorySwap(bool showHint = true)
    {
        if (!_MemSwapLock.Wait(0))
        {
            Context.Warn("检测到正在进行的内存处理，取消当前处理");
            if (showHint) HintWrapper.Show("内存优化尚未结束，请稍等！");
            return;
        }
        Context.Info("收到内存交换请求，开始处理");
        if (showHint) HintWrapper.Show("正在进行内存优化");
        try
        {
            var before = KernelInterop.GetAvailablePhysicalMemoryBytes();
            Context.Info($"处理前内存量 {ByteStream.GetReadableLength((long)before)}");
            
            // 开始内存处理
            PromoteService.Append("mem-swap", result =>
            {
                if (result == null)
                {
                    var after = KernelInterop.GetAvailablePhysicalMemoryBytes();
                    Context.Info($"处理后内存量 {ByteStream.GetReadableLength((long)after)}");
                    var diff = Math.Max(0, after - before);
                    var afterStr = ByteStream.GetReadableLength((long)after);
                    var diffStr = ByteStream.GetReadableLength((long)diff);
                    Context.Info($"处理结束，总共处理 {diffStr}");
                    if (showHint) MsgBoxWrapper.Show($"内存优化结束，共优化 {diffStr}，目前可用内存 {afterStr}");
                }
                else
                {
                    Context.Error($"内存优化失败\n\n详细信息: {result}", actionLevel: ActionLevel.MsgBoxErr);
                }
                _MemSwapLock.Release();
            });
            if (!PromoteService.Activate() && showHint)
            {
                MsgBoxWrapper.Show("提权进程启动失败，请允许管理员权限以使用内存优化", "内存优化失败");
            }
        }
        catch (Exception ex)
        {
            Context.Error("内存优化失败", ex, ActionLevel.MsgBoxErr);
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
