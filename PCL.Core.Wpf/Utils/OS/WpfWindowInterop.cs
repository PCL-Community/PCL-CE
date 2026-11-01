using System.Runtime.InteropServices;
using System.Text;
using PCL.Core.Platform;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Wpf;

/// <summary>基于 Win32 user32 的游戏窗口交互实现。</summary>
public sealed class WpfWindowInterop : IWindowInterop
{
    public (nint Handle, string Title)? TryFindGameWindow(int processId)
    {
        (nint Handle, string Title)? foundWindow = null;

        EnumWindows(
            (windowHandle, _) =>
            {
                if (foundWindow is not null)
                    return false;

                if (!TryGetMinecraftWindowInfo(windowHandle, processId, out var windowTitle))
                    return true;

                foundWindow = (windowHandle, windowTitle);
                return false;
            },
            nint.Zero);

        return foundWindow;
    }

    public void SetWindowTitle(nint handle, string title)
    {
        SetWindowText(handle, title);
    }

    public void MaximizeWindow(nint handle)
    {
        ShowWindow(handle, 3U);
    }

    private static bool TryGetMinecraftWindowInfo(
        nint windowHandle,
        int processId,
        out string windowTitle)
    {
        var buffer = new StringBuilder(512);
        _ = GetClassName(windowHandle, buffer, buffer.Capacity);

        var className = buffer.ToString();
        if (className is not ("GLFW30" or "LWJGL" or "SunAwtFrame"))
        {
            windowTitle = string.Empty;
            return false;
        }

        buffer = new StringBuilder(512);
        _ = GetWindowText(windowHandle, buffer, buffer.Capacity);

        windowTitle = buffer.ToString();

        if (!(windowTitle.StartsWithF("FML")
                || (windowTitle != "PopupMessageWindow"
                    && !windowTitle.StartsWithF("GLFW"))))
            return false;

        var windowProcessId = 0;
        _ = GetWindowThreadProcessId(windowHandle, ref windowProcessId);

        return windowProcessId == processId;
    }

    private delegate bool EnumWindowsSub(nint hwnd, nint lParam);

    [DllImport("user32")]
    private static extern bool EnumWindows(
        EnumWindowsSub lpEnumFunc,
        nint lParam);

    [DllImport("user32", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(
        nint hWnd,
        StringBuilder lpClassName,
        int nMaxCount);

    [DllImport("user32", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(
        nint hWnd,
        StringBuilder lpString,
        int nMaxCount);

    [DllImport("user32", EntryPoint = "SetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowText(nint hWnd, string lpString);

    [DllImport("user32")]
    private static extern bool ShowWindow(nint hWnd, uint cmdWindow);

    [DllImport("user32")]
    private static extern int GetWindowThreadProcessId(
        nint hWnd,
        ref int lpdwProcessId);
}