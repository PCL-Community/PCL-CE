using System.Runtime.InteropServices;
using System.Text;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>
///     游戏窗口交互的 UI 侧实现：Windows 使用 user32，Linux 使用 X11，其他平台不支持。
///     所有平台调用都做了异常兜底，失败时返回 <c>null</c> 或静默跳过，不向业务层抛异常。
/// </summary>
public sealed class WindowInterop : IWindowInterop
{
    private const string ModuleName = "WindowInterop";

    public (nint Handle, string Title)? TryFindGameWindow(int processId)
    {
        try
        {
            if (OperatingSystem.IsWindows()) return _TryFindGameWindowWindows(processId);

            if (OperatingSystem.IsLinux()) return _TryFindGameWindowX11(processId);

            LogWrapper.Debug(ModuleName, "当前平台不支持查找游戏窗口");
            return null;
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ModuleName, $"查找游戏窗口失败：{ex.Message}");
            return null;
        }
    }

    public void SetWindowTitle(nint handle, string title)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                _SetWindowTitleWindows(handle, title);
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                _SetWindowTitleX11(handle, title);
                return;
            }

            LogWrapper.Debug(ModuleName, "当前平台不支持设置窗口标题");
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ModuleName, $"设置窗口标题失败：{ex.Message}");
        }
    }

    public void MaximizeWindow(nint handle)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                _MaximizeWindowWindows(handle);
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                _MaximizeWindowX11(handle);
                return;
            }

            LogWrapper.Debug(ModuleName, "当前平台不支持最大化窗口");
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ModuleName, $"最大化窗口失败：{ex.Message}");
        }
    }

    #region Windows (user32)

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

    private delegate bool EnumWindowsSub(nint hwnd, nint lParam);

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

    #endregion

    #region Linux (X11)

    private const int X11IsViewable = 2;
    private const int X11MaxWindowDepth = 6;
    private const int X11ClientMessage = 33;
    private const long X11SubstructureNotifyMask = 1L << 19;
    private const long X11SubstructureRedirectMask = 1L << 20;
    private const int X11PropModeReplace = 0;
    private const int X11FormatByte = 8;
    private const int X11FormatLong = 32;

    private static (nint Handle, string Title)? _TryFindGameWindowX11(int processId)
    {
        var display = _X11OpenDisplay();
        if (display == 0) return null;

        try
        {
            var root = XDefaultRootWindow(display);
            if (root == 0) return null;

            var candidates = new List<(nint Handle, string Title, int Score)>();
            _X11WalkWindows(display, root, processId, candidates, 0);

            if (candidates.Count == 0)
            {
                LogWrapper.Debug(ModuleName, $"未在 X11 中找到进程 {processId} 的窗口");
                return null;
            }

            var best = candidates[0];
            foreach (var candidate in
                     candidates.Where(candidate => candidate.Score > best.Score))
                best = candidate;

            return (best.Handle, best.Title);
        }
        finally
        {
            _ = XCloseDisplay(display);
        }
    }

    private static void _X11WalkWindows(
        nint display,
        nint parent,
        int processId,
        List<(nint Handle, string Title, int Score)> candidates,
        int depth)
    {
        if (depth > X11MaxWindowDepth) return;

        if (XQueryTree(
                display,
                parent,
                out _,
                out _,
                out var children,
                out var childCount)
            == 0)
            return;

        try
        {
            for (var i = 0; i < childCount; i++)
            {
                var window = Marshal.ReadIntPtr(children, i * IntPtr.Size);
                if (window == 0) continue;

                if (_X11GetWindowAttributes(display, window, out var attributes)
                    && attributes is { map_state: X11IsViewable, width: > 1, height: > 1 }
                    && _X11GetWindowPid(display, window) == processId)
                {
                    var title = _X11GetWindowTitle(display, window);
                    var windowClass = _X11GetWindowClass(display, window);
                    candidates.Add((window, title, _X11ScoreGameWindow(windowClass, title)));
                }

                _X11WalkWindows(display, window, processId, candidates, depth + 1);
            }
        }
        finally
        {
            if (children != 0) _ = XFree(children);
        }
    }

    /// <summary>优先选择类名或标题符合 Minecraft 特征的窗口，其次才是任意匹配进程的可见窗口。</summary>
    private static int _X11ScoreGameWindow(string windowClass, string title)
    {
        return 1
            + new[]
                {
                    "minecraft",
                    "java",
                    "glfw",
                    "lwjgl"
                }
                .Where(keyword => windowClass.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Sum(_ => 2)
            + new[]
                {
                    "minecraft",
                    "fml",
                    "forge",
                    "quilt",
                    "glfw",
                    "lwjgl"
                }
                .Where(keyword => title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Sum(_ => 2);
    }

    private static void _SetWindowTitleX11(nint handle, string title)
    {
        var display = _X11OpenDisplay();
        if (display == 0) return;

        try
        {
            // EWMH 使用 UTF-8 编码的 _NET_WM_NAME；WM_NAME 作为不识别 EWMH 的窗口管理器的兜底
            var utf8Title = Encoding.UTF8.GetBytes(title);
            var netWmName = XInternAtom(display, "_NET_WM_NAME", 0);
            var utf8String = XInternAtom(display, "UTF8_STRING", 0);
            if (netWmName != 0 && utf8String != 0)
                _ = XChangeProperty(
                    display,
                    handle,
                    netWmName,
                    utf8String,
                    X11FormatByte,
                    X11PropModeReplace,
                    utf8Title,
                    utf8Title.Length);

            _ = XStoreName(display, handle, title);
            _ = XFlush(display);
        }
        finally
        {
            _ = XCloseDisplay(display);
        }
    }

    private static void _MaximizeWindowX11(nint handle)
    {
        var display = _X11OpenDisplay();
        if (display == 0) return;

        try
        {
            var root = XDefaultRootWindow(display);
            _ = XMapRaised(display, handle);

            // 通过 EWMH 请求窗口管理器把窗口最大化（X11 下最大化必须由 WM 完成）
            var wmState = XInternAtom(display, "_NET_WM_STATE", 0);
            var maximizedVert = XInternAtom(display, "_NET_WM_STATE_MAXIMIZED_VERT", 0);
            var maximizedHorz = XInternAtom(display, "_NET_WM_STATE_MAXIMIZED_HORZ", 0);
            if (root != 0 && wmState != 0 && maximizedVert != 0 && maximizedHorz != 0)
            {
                var clientMessage = new XClientMessageEvent
                {
                    type = X11ClientMessage,
                    send_event = 1,
                    display = display,
                    window = handle,
                    message_type = wmState,
                    format = X11FormatLong,
                    data0 = 1, // _NET_WM_STATE_ADD
                    data1 = maximizedVert,
                    data2 = maximizedHorz,
                    data3 = 1, // source indication: normal application
                    data4 = 0
                };
                _ = XSendEvent(
                    display,
                    root,
                    0,
                    X11SubstructureNotifyMask | X11SubstructureRedirectMask,
                    ref clientMessage);
            }

            _ = XFlush(display);
        }
        finally
        {
            _ = XCloseDisplay(display);
        }
    }

    private static nint _X11OpenDisplay()
    {
        var display = XOpenDisplay(null);
        if (display == 0) LogWrapper.Debug(ModuleName, "无法连接 X11 显示（DISPLAY 未设置或不可用）");
        return display;
    }

    private static int _X11GetWindowPid(nint display, nint window)
    {
        var property = XInternAtom(display, "_NET_WM_PID", 1);
        if (property == 0) return -1;

        if (XGetWindowProperty(
                display,
                window,
                property,
                0,
                1,
                0,
                nint.Zero,
                out _,
                out var actualFormat,
                out var itemCount,
                out _,
                out var data)
            != 0
            || data == 0)
            return -1;

        try
        {
            if (actualFormat != X11FormatLong || itemCount == 0) return -1;
            return (int)Marshal.ReadInt64(data);
        }
        finally
        {
            _ = XFree(data);
        }
    }

    private static string _X11GetWindowTitle(nint display, nint window)
    {
        var netWmName = XInternAtom(display, "_NET_WM_NAME", 1);
        var utf8String = XInternAtom(display, "UTF8_STRING", 1);
        if (netWmName != 0
            && utf8String != 0
            && XGetWindowProperty(
                display,
                window,
                netWmName,
                0,
                1024,
                0,
                utf8String,
                out var actualType,
                out var actualFormat,
                out var itemCount,
                out _,
                out var data)
            == 0
            && data != 0)
            try
            {
                if (actualType == utf8String
                    && actualFormat == X11FormatByte
                    && itemCount > 0)
                    return _X11ReadUtf8(data, (int)itemCount);
            }
            finally
            {
                _ = XFree(data);
            }

        if (XFetchName(display, window, out var name) == 0 || name == 0) return string.Empty;

        try
        {
            return Marshal.PtrToStringAnsi(name) ?? string.Empty;
        }
        finally
        {
            _ = XFree(name);
        }
    }

    private static string _X11GetWindowClass(nint display, nint window)
    {
        var wmClass = XInternAtom(display, "WM_CLASS", 1);
        if (wmClass == 0
            || XGetWindowProperty(
                display,
                window,
                wmClass,
                0,
                1024,
                0,
                nint.Zero,
                out _,
                out var actualFormat,
                out var itemCount,
                out _,
                out var data)
            != 0
            || data == 0) return string.Empty;

        try
        {
            if (actualFormat != X11FormatByte || itemCount == 0) return string.Empty;
            var raw = _X11ReadUtf8(data, (int)itemCount);
            var parts = raw.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? string.Empty : parts[^1];
        }
        finally
        {
            _ = XFree(data);
        }
    }

    private static string _X11ReadUtf8(nint data, int byteCount)
    {
        var buffer = new byte[byteCount];
        Marshal.Copy(data, buffer, 0, byteCount);
        return Encoding.UTF8.GetString(buffer).TrimEnd('\0');
    }

    private static bool _X11GetWindowAttributes(
        nint display,
        nint window,
        out XWindowAttributes attributes)
    {
        attributes = default;
        try
        {
            return XGetWindowAttributes(display, window, ref attributes) != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XWindowAttributes
    {
        public int x;
        public int y;
        public int width;
        public int height;
        public int border_width;
        public int depth;
        public nint visual;
        public nint root;
        public int @class;
        public int bit_gravity;
        public int win_gravity;
        public int backing_store;
        public nint backing_planes;
        public nint backing_pixel;
        public int save_under;
        public nint colormap;
        public int map_installed;
        public int map_state;
        public nint all_event_masks;
        public nint your_event_mask;
        public nint do_not_propagate_mask;
        public int override_redirect;
        public nint screen;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XClientMessageEvent
    {
        public int type;
        public nuint serial;
        public int send_event;
        public nint display;
        public nint window;
        public nint message_type;
        public int format;
        public nint data0;
        public nint data1;
        public nint data2;
        public nint data3;
        public nint data4;
    }

    [DllImport("libX11.so.6")]
    private static extern nint XOpenDisplay(string? displayName);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(nint display);

    [DllImport("libX11.so.6")]
    private static extern nint XDefaultRootWindow(nint display);

    [DllImport("libX11.so.6")]
    private static extern nint XInternAtom(nint display, string atomName, int onlyIfExists);

    [DllImport("libX11.so.6")]
    private static extern int XQueryTree(
        nint display,
        nint window,
        out nint rootReturn,
        out nint parentReturn,
        out nint childrenReturn,
        out uint childCountReturn);

    [DllImport("libX11.so.6")]
    private static extern int XFree(nint data);

    [DllImport("libX11.so.6")]
    private static extern int XGetWindowProperty(
        nint display,
        nint window,
        nint property,
        long longOffset,
        long longLength,
        int delete,
        nint requiredType,
        out nint actualType,
        out int actualFormat,
        out nuint itemCount,
        out nuint bytesAfter,
        out nint data);

    [DllImport("libX11.so.6")]
    private static extern int XGetWindowAttributes(
        nint display,
        nint window,
        ref XWindowAttributes attributes);

    [DllImport("libX11.so.6")]
    private static extern int XChangeProperty(
        nint display,
        nint window,
        nint property,
        nint type,
        int format,
        int mode,
        byte[] data,
        int itemCount);

    [DllImport("libX11.so.6")]
    private static extern int XSendEvent(
        nint display,
        nint window,
        int propagate,
        long eventMask,
        ref XClientMessageEvent eventData);

    [DllImport("libX11.so.6")]
    private static extern int XMapRaised(nint display, nint window);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(nint display);

    [DllImport("libX11.so.6")]
    private static extern int XStoreName(nint display, nint window, string windowName);

    [DllImport("libX11.so.6")]
    private static extern int XFetchName(nint display, nint window, out nint windowName);

    #endregion
}