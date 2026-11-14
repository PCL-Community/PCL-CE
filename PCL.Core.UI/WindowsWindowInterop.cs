using System.Runtime.InteropServices;
using System.Text;

namespace PCL.Core.UI;

/// <summary>Windows user32 游戏窗口实现。</summary>
public sealed class WindowsWindowInterop : WindowInteropBase
{
    protected override (nint Handle, string Title)? FindGameWindowCore(int processId)
    {
        return _TryFindGameWindowWindows(processId);
    }

    protected override void SetWindowTitleCore(nint handle, string title)
    {
        _SetWindowTitleWindows(handle, title);
    }

    protected override void MaximizeWindowCore(nint handle)
    {
        _MaximizeWindowWindows(handle);
    }

    private static (nint Handle, string Title)? _TryFindGameWindowWindows(int processId)
    {
        (nint Handle, string Title)? foundWindow = null;

        EnumWindows(
            (windowHandle, _) =>
            {
                if (foundWindow is not null) return false;

                if (!_TryGetMinecraftWindowInfoWindows(
                        windowHandle,
                        processId,
                        out var windowTitle))
                    return true;

                foundWindow = (windowHandle, windowTitle);
                return false;
            },
            nint.Zero);

        return foundWindow;
    }

    private static bool _TryGetMinecraftWindowInfoWindows(
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

        var isLoaderWindow = windowTitle.StartsWith("FML", StringComparison.Ordinal)
            || windowTitle.StartsWith("Quilt Loader", StringComparison.Ordinal);
        if (!isLoaderWindow
            && (windowTitle == "PopupMessageWindow"
                || windowTitle.StartsWith("GLFW", StringComparison.Ordinal)))
            return false;

        var windowProcessId = 0;
        _ = GetWindowThreadProcessId(windowHandle, ref windowProcessId);
        return windowProcessId == processId;
    }

    private static void _SetWindowTitleWindows(nint handle, string title)
    {
        _ = SetWindowText(handle, title);
    }

    private static void _MaximizeWindowWindows(nint handle)
    {
        // SW_MAXIMIZE = 3
        _ = ShowWindow(handle, 3U);
    }

    [DllImport("user32")]
    private static extern bool EnumWindows(EnumWindowsSub lpEnumFunc, nint lParam);

    [DllImport("user32", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32", EntryPoint = "SetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowText(nint hWnd, string lpString);

    [DllImport("user32")]
    private static extern bool ShowWindow(nint hWnd, uint cmdWindow);

    [DllImport("user32")]
    private static extern int GetWindowThreadProcessId(nint hWnd, ref int lpdwProcessId);

    private delegate bool EnumWindowsSub(nint hwnd, nint lParam);
}