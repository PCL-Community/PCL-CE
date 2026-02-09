// reshaper disable all

#pragma warning disable all

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;

namespace PCL.Core.Utils.OS;

/// <summary>
/// 文件拖拽修复 <br/>
/// 这堆代码本质上是利用资源管理器还在发 WindowMessage 来实现的
/// </summary>
public sealed class DragHelper
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

        // 管理员进程下撤销 OLE DragDrop
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
        if (TryGetDropInfo(msg, wParam, out var files, out var DragPoint))
        {
            DropFilePaths = files;
            DropDragPoint = DragPoint;

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
        bool isVistaOrHigher = ver >= new Version(6, 0);
        bool isWin7OrHigher = ver >= new Version(6, 1);

        if (!isVistaOrHigher)
            return;

        var filter = new CHANGEFILTERSTRUCT
        {
            cbSize = (uint)Marshal.SizeOf<CHANGEFILTERSTRUCT>()
        };

        uint[] messages =
        {
            WM_DROPFILES,
            WM_COPYGLOBALDATA,
            WM_COPYDATA
        };

        foreach (uint msg in messages)
        {
            bool success = isWin7OrHigher
                ? ChangeWindowMessageFilterEx(hwnd, msg, MSGFLT_ALLOW, ref filter)
                : ChangeWindowMessageFilter(msg, MSGFLT_ADD);

            if (!success)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    #endregion

    #region Drop parsing

    private static bool TryGetDropInfo(
        int msg,
        IntPtr hDrop,
        out string[]? filePaths,
        out DragPoint dropDragPoint)
    {
        filePaths = null;
        dropDragPoint = default;

        if (msg != WM_DROPFILES)
            return false;

        uint count = DragQueryFile(hDrop, uint.MaxValue, null, 0);
        filePaths = new string[count];

        for (uint i = 0; i < count; i++)
        {
            var sb = new StringBuilder(MAX_PATH);
            if (DragQueryFile(hDrop, i, sb, sb.Capacity) > 0)
                filePaths[i] = sb.ToString();
        }

        DragQueryPoint(hDrop, out dropDragPoint);
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

    private const int MAX_PATH = 260;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilter(
        uint msg,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilterEx(
        IntPtr hwnd,
        uint msg,
        uint action,
        ref CHANGEFILTERSTRUCT filter);

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(
        IntPtr hwnd,
        bool accept);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(
        IntPtr hDrop,
        uint iFile,
        StringBuilder? fileName,
        int cch);

    [DllImport("shell32.dll")]
    private static extern bool DragQueryPoint(
        IntPtr hDrop,
        out DragPoint pt);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(
        IntPtr hDrop);

    [DllImport("ole32.dll")]
    private static extern int RevokeDragDrop(
        IntPtr hwnd);

    [DllImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsUserAnAdmin();

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
