using System;
using System.IO;
using System.Runtime.InteropServices;
using PCL.Core.Platform;

namespace PCL.Core.IO;

public sealed class WindowsRecycleBinService : IRecycleBinService
{
    private const uint FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;

    public bool IsSupported(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path));
    }

    public RecycleBinOutcome Move(string? path, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "路径为空";
            return RecycleBinOutcome.Failed;
        }

        if (!IsSupported(path))
        {
            error = "路径不存在";
            return RecycleBinOutcome.Failed;
        }

        try
        {
            var op = new ShFileOpStruct
            {
                wFunc = FoDelete,
                pFrom = $"{path}\0\0",
                fFlags = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi
            };
            var result = SHFileOperation(ref op);
            if (result == 0 && !op.fAnyOperationsAborted)
                return RecycleBinOutcome.Moved;
            error = $"SHFileOperation 返回 {result}";
            return RecycleBinOutcome.Failed;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return RecycleBinOutcome.Failed;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref ShFileOpStruct fileOp);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public nint hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public nint hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }
}