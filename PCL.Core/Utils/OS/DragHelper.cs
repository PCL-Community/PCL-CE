using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PCL.Core.Utils.OS;

// ReSharper disable InconsistentNaming
public partial class DragHelper
{
    public event EventHandler? DragDrop;

    public string[]? DropFilePaths { get; private set; }
    public Point DropDragPoint { get; private set; }

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
            DropDragPoint = new Point(pt.X, pt.Y);
            DragDrop?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    #endregion

    #region Message filter (UAC)

    private static unsafe void ChangeMessageFilter(IntPtr hwnd)
    {
        var ver = Environment.OSVersion.Version;
        if (ver < new Version(6, 0))
            return;

        var win7OrHigher = ver >= new Version(6, 1);

        var filter = new CHANGEFILTERSTRUCT
        {
            cbSize = (uint)sizeof(CHANGEFILTERSTRUCT)
        };

        uint[] messages = [
            WM_DROPFILES,
            WM_COPYGLOBALDATA,
            WM_COPYDATA
        ];

        foreach (var msg in messages)
        {
            var ok = win7OrHigher
                ? ChangeWindowMessageFilterEx(hwnd, msg, MSGFLT_ALLOW, ref filter)
                : ChangeWindowMessageFilter(msg, MSGFLT_ADD);

            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
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

        var count = DragQueryFile(hDrop, uint.MaxValue, IntPtr.Zero, 0);
        filePaths = new string[count];

        const int maxPath = 32768, smallerMaxPath = 1024;

        Span<char> gBuffer = stackalloc char[smallerMaxPath];
        for (uint i = 0; i < count; i++)
        {
            var len = DragQueryFile(hDrop, i, IntPtr.Zero, 0) + 1;
            if (len > maxPath) len = maxPath;
            var buffer = len <= smallerMaxPath ? gBuffer[..(int)len] : new char[len];
            _ = DragQueryFile(hDrop, i, buffer, len);
            filePaths[i] = new string(buffer[..(int)(len - 1)]);
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

    [LibraryImport("shell32.dll", EntryPoint = "DragQueryFileW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint DragQueryFile(IntPtr hDrop, uint iFile, Span<char> lpszFile, uint cch);
    
    [LibraryImport("shell32.dll", EntryPoint = "DragQueryFileW")]
    private static partial uint DragQueryFile(IntPtr hDrop, uint iFile, IntPtr lpszFile, uint cch);

    [LibraryImport("shell32.dll")]
    private static partial void DragFinish(IntPtr hDrop);

    [LibraryImport("ole32.dll")]
    private static partial int RevokeDragDrop(IntPtr hwnd);

    [LibraryImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsUserAnAdmin();

    #endregion

    #region Structs

    [StructLayout(LayoutKind.Sequential)]
    private struct DragPoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CHANGEFILTERSTRUCT
    {
        public uint cbSize;
        public uint ExtStatus;
    }

    #endregion
}
