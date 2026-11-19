using System.Runtime.InteropServices;

namespace PCL.Platform;

/// <summary>Windows 原生致命错误对话框：使用 user32 MessageBoxW。</summary>
public sealed class WindowsFatalDialogService : FatalDialogServiceBase
{
    private const uint MbIconError = 0x00000010u;
    private const uint MbSetForeground = 0x00010000u;
    private const uint MbTopmost = 0x00040000u;

    protected override bool ShowFatalCore(string message, string caption)
    {
        return MessageBoxW(
                IntPtr.Zero,
                message,
                caption,
                MbIconError | MbSetForeground | MbTopmost)
            != 0;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(
        IntPtr hWnd,
        string text,
        string caption,
        uint type);
}