using System;
using System.Runtime.InteropServices;
using PCL.Core.Utils.OS;

namespace PCL.Core.App.Tools;

[Flags]
public enum MemSwapScope
{
    None = 0,
    EmptyWorkingSets = 1 << 0,
    FlushFileCache = 1 << 1,
    FlushModifiedList = 1 << 2,
    PurgeStandbyList = 1 << 3,
    PurgeLowPriorityStandbyList = 1 << 4,
    RegistryReconciliation = 1 << 5,
    CombinePhysicalMemory = 1 << 6,
    All = 0b111111
}

// ReSharper disable InconsistentNaming
internal static class MemSwapWorks
{
    private static void _ExecuteMemoryListOperation(int infoValue)
    {
        GCHandle handle = GCHandle.Alloc(infoValue, GCHandleType.Pinned);
        try
        {
            NtInterop.SetSystemInformation(
                NtInterop.SystemInformationClass.SystemMemoryListInformation,
                handle.AddrOfPinnedObject(),
                sizeof(int));
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }
    }

    private static void _ExecuteStructureOperation<T>(T structure, NtInterop.SystemInformationClass infoClass)
    {
        GCHandle handle = GCHandle.Alloc(structure, GCHandleType.Pinned);
        try
        {
            NtInterop.SetSystemInformation(
                infoClass,
                handle.AddrOfPinnedObject(),
                (uint)Marshal.SizeOf(structure));
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }
    }

    public static void EmptyWorkingSets() => _ExecuteMemoryListOperation(2);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_FILECACHE_INFORMATION
    {
        public UIntPtr CurrentSize;
        public UIntPtr PeakSize;
        public UIntPtr PageFaultCount;
        public UIntPtr MinimumWorkingSet;
        public UIntPtr MaximumWorkingSet;
        public UIntPtr CurrentSizeIncludingTransitionInPages;
        public UIntPtr PeakSizeIncludingTransitionInPages;
        public UIntPtr TransitionRePurposeCount;
        public UIntPtr Flags;
    }

    public static void FlushFileCache()
    {
        var scfi = new SYSTEM_FILECACHE_INFORMATION
        {
            MaximumWorkingSet = uint.MaxValue,
            MinimumWorkingSet = uint.MaxValue
        };
        _ExecuteStructureOperation(scfi, NtInterop.SystemInformationClass.SystemFileCacheInformationEx);
    }

    public static void FlushModifiedList() => _ExecuteMemoryListOperation(3);

    public static void PurgeStandbyList() => _ExecuteMemoryListOperation(4);

    internal static void PurgeLowPriorityStandbyList() => _ExecuteMemoryListOperation(5);

    public static void RegistryReconciliation() =>
        NtInterop.SetSystemInformation(
            NtInterop.SystemInformationClass.SystemRegistryReconciliationInformation,
            IntPtr.Zero,
            0);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_COMBINE_INFORMATION_EX
    {
        public IntPtr Handle;
        public UIntPtr PagesCombined;
        public uint Flags;
    }

    public static void CombinePhysicalMemory()
    {
        var combineInfoEx = new MEMORY_COMBINE_INFORMATION_EX();
        _ExecuteStructureOperation(combineInfoEx, NtInterop.SystemInformationClass.SystemCombinePhysicalMemoryInformation);
    }
}
