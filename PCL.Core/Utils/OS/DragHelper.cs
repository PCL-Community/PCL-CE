// reshaper disable all
#pragma warning disable all

using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PCL.Core.Utils.OS;

public unsafe partial class DragHelper
{
    public event EventHandler? DragDrop;

    public string[]? DropFilePaths { get; private set; }
    public DragPoint DropDragPoint { get; private set; }

    public HwndSource? HwndSource { get; set; }

    #region Public API

    public void AddHook()
    {
        if (HwndSource is null)
            throw new InvalidOperationException("HwndSource 未设置");

        RemoveHook();

        HwndSource.AddHook(WndProc);
        IntPtr hwnd = HwndSource.Handle;

        if (IsUserAnAdmin())
            RevokeDragDrop(hwnd);

        DragAcceptFiles(hwnd, true);
        ChangeMessageFilter(hwnd);
    }

    public void RemoveHook()
    {
        if (HwndSource is null)
            return;

        HwndSource.RemoveHook(WndProc);
        DragAcceptFiles(HwndSource.Handle, false);
    }

    #endregion

    #region WndProc

    private IntPtr WndProc(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (TryGetDropInfo(msg, wParam, out var files, out var pt))
        {
            DropFilePaths = files;
            DropDragPoint = pt;
            DragDrop?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    #endregion

    #region Message filter (UAC)

    private static void ChangeMessageFilter(IntPtr hwnd)
    {
        Version ver = Environment.OSVersion.Version;
        if (ver < new Version(6, 0))
            return;

        bool win7OrHigher = ver >= new Version(6, 1);

        var filter = new CHANGEFILTERSTRUCT
        {
            cbSize = (uint)sizeof(CHANGEFILTERSTRUCT)
        };

        uint[] messages =
        {
            WM_DROPFILES,
            WM_COPYGLOBALDATA,
            WM_COPYDATA
        };

        foreach (uint msg in messages)
        {
            bool ok = win7OrHigher
                ? ChangeWindowMessageFilterEx(hwnd, msg, MSGFLT_ALLOW, ref filter)
                : ChangeWindowMessageFilter(msg, MSGFLT_ADD);

            if (!ok)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    #endregion

    #region Drop parsing

    private static bool TryGetDropInfo(
        int msg,
        IntPtr hDrop,
        out string[]? filePaths,
        out DragPoint dropPoint)
    {
        filePaths = null;
        dropPoint = default;

        if (msg != WM_DROPFILES)
            return false;

        var count = DragQueryFile(hDrop, uint.MaxValue, out _, 0);
        filePaths = new string[count];
        
        for (uint i = 0; i < count; i++)
        {
            var len = DragQueryFile(hDrop, i, out _, 0);
            DragQueryFile(hDrop, i, out var pathPtr, len + 1);
            filePaths[i] = Marshal.PtrToStringUTF8(pathPtr) ?? "";
        }

        DragFinish(hDrop);
        return true;
    }

    #endregion

    #region Win32

    private const uint WM_COPYGLOBALDATA = 0x0049;
    private const uint WM_COPYDATA = 0x004A;
    private const uint WM_DROPFILES = 0x0233;

    private const uint MSGFLT_ALLOW = 1;
    private const uint MSGFLT_ADD = 1;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeWindowMessageFilter(
        uint msg,
        uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeWindowMessageFilterEx(
        IntPtr hwnd,
        uint msg,
        uint action,
        ref CHANGEFILTERSTRUCT filter);

    [LibraryImport("shell32.dll")]
    private static partial void DragAcceptFiles(
        IntPtr hwnd,
        [MarshalAs(UnmanagedType.Bool)] bool accept);

    [LibraryImport("shell32.dll", EntryPoint = "DragQueryFileW")]
    private static partial uint DragQueryFile(
        IntPtr hDrop,
        uint iFile,
        out IntPtr lpszFile,
        uint cch);

    [LibraryImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DragQueryPoint(
        IntPtr hDrop,
        out DragPoint pt);

    [LibraryImport("shell32.dll")]
    private static partial void DragFinish(
        IntPtr hDrop);

    [LibraryImport("ole32.dll")]
    private static partial int RevokeDragDrop(
        IntPtr hwnd);

    [LibraryImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsUserAnAdmin();

    #endregion
}

#region Structs

[StructLayout(LayoutKind.Sequential)]
public struct DragPoint
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CHANGEFILTERSTRUCT
{
    public uint cbSize;
    public uint ExtStatus;
}

#endregion
