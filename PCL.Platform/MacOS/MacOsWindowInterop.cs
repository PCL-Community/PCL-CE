using System.Runtime.InteropServices;
using System.Text;
using PCL.Core.Logging;

namespace PCL.Platform;

public sealed class MacOsWindowInterop : WindowInteropBase
{
    private const string ApplicationServices =
        "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    private const string CoreFoundation =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    private const uint Utf8Encoding = 0x08000100;
    private const int MaxTitleLength = 1024;
    private static nint _windowsAttribute;
    private static nint _titleAttribute;
    private static nint _zoomButtonAttribute;
    private static nint _pressAction;
    private static nint _retainedWindow;
    private static int _untrustedLogged;

    private static nint WindowsAttribute =>
        _windowsAttribute != 0
            ? _windowsAttribute
            : _windowsAttribute = _CreateAttribute("AXWindows");

    private static nint TitleAttribute =>
        _titleAttribute != 0 ? _titleAttribute : _titleAttribute = _CreateAttribute("AXTitle");

    private static nint ZoomButtonAttribute =>
        _zoomButtonAttribute != 0
            ? _zoomButtonAttribute
            : _zoomButtonAttribute = _CreateAttribute("AXZoomButton");

    private static nint PressAction =>
        _pressAction != 0 ? _pressAction : _pressAction = _CreateAttribute("AXPress");

    protected override (nint Handle, string Title)? FindGameWindowCore(int processId)
    {
        if (!AXIsProcessTrusted())
        {
            if (Interlocked.Exchange(ref _untrustedLogged, 1) == 0)
                LogWrapper.Warn(
                    "WindowInterop",
                    "未获得辅助功能权限，无法查找或最大化 macOS 游戏窗口");

            return null;
        }

        var application = AXUIElementCreateApplication(processId);
        if (application == 0) return null;

        nint windows = 0;
        try
        {
            if (AXUIElementCopyAttributeValue(
                    application,
                    WindowsAttribute,
                    out windows)
                != 0
                || windows == 0)
                return null;

            var count = CFArrayGetCount(windows);
            (nint Handle, string Title, int Score)? best = null;

            for (nint i = 0; i < count; i++)
            {
                var window = CFArrayGetValueAtIndex(windows, i);
                if (window == 0) continue;

                var title = _ReadTitle(window);
                var score = _ScoreWindow(title);
                if (score < 0) continue;

                if (best is null || score > best.Value.Score)
                    best = (window, title, score);
            }

            if (best is null) return null;

            var retained = CFRetain(best.Value.Handle);
            var previous = Interlocked.Exchange(ref _retainedWindow, retained);
            if (previous != 0) CFRelease(previous);

            return (retained, best.Value.Title);
        }
        finally
        {
            if (windows != 0) CFRelease(windows);
            CFRelease(application);
        }
    }

    protected override void SetWindowTitleCore(nint handle, string title)
    {
        var titleString = _CreateString(title);
        if (titleString == 0) return;

        try
        {
            _ = AXUIElementSetAttributeValue(handle, TitleAttribute, titleString);
        }
        finally
        {
            CFRelease(titleString);
        }
    }

    protected override void MaximizeWindowCore(nint handle)
    {
        if (AXUIElementCopyAttributeValue(handle, ZoomButtonAttribute, out var zoomButton) == 0
            && zoomButton != 0)
            try
            {
                // 触发 macOS 原生绿色缩放按钮，行为比手动改 AXSize 更接近“最大化”。
                if (AXUIElementPerformAction(zoomButton, PressAction) == 0) return;
            }
            finally
            {
                CFRelease(zoomButton);
            }

        LogWrapper.Debug("WindowInterop", "macOS 游戏窗口没有可用的缩放按钮，已跳过最大化");
    }

    private static string _ReadTitle(nint window)
    {
        if (AXUIElementCopyAttributeValue(window, TitleAttribute, out var value) != 0
            || value == 0)
            return string.Empty;

        try
        {
            var buffer = new byte[MaxTitleLength];
            return CFStringGetCString(
                value,
                buffer,
                buffer.Length,
                Utf8Encoding)
                ? _DecodeUtf8(buffer)
                : string.Empty;
        }
        finally
        {
            CFRelease(value);
        }
    }

    private static int _ScoreWindow(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return 1;
        if (title.StartsWith("FML", StringComparison.Ordinal)
            || title.StartsWith("Quilt Loader", StringComparison.Ordinal))
            return -1;

        var score = 1;
        if (title.Contains("minecraft", StringComparison.OrdinalIgnoreCase)) score += 5;
        if (title.Contains("forge", StringComparison.OrdinalIgnoreCase)) score += 3;
        if (title.Contains("fabric", StringComparison.OrdinalIgnoreCase)) score += 3;
        if (title.Contains("neoforge", StringComparison.OrdinalIgnoreCase)) score += 3;
        if (title.Contains("quilt", StringComparison.OrdinalIgnoreCase)) score += 3;
        if (title.Contains("glfw", StringComparison.OrdinalIgnoreCase)) score += 1;
        if (title.Contains("lwjgl", StringComparison.OrdinalIgnoreCase)) score += 1;
        return score;
    }

    private static string _DecodeUtf8(byte[] buffer)
    {
        var length = Array.IndexOf(buffer, (byte)0);
        return Encoding.UTF8.GetString(
            buffer,
            0,
            length < 0 ? buffer.Length : length);
    }

    private static nint _CreateAttribute(string value)
    {
        return _CreateString(value);
    }

    private static nint _CreateString(string value)
    {
        return CFStringCreateWithCString(0, value, Utf8Encoding);
    }

    [DllImport(ApplicationServices)]
    private static extern nint AXUIElementCreateApplication(int pid);

    [DllImport(ApplicationServices)]
    private static extern int AXUIElementCopyAttributeValue(
        nint element,
        nint attribute,
        out nint value);

    [DllImport(ApplicationServices)]
    private static extern int AXUIElementSetAttributeValue(
        nint element,
        nint attribute,
        nint value);

    [DllImport(ApplicationServices)]
    private static extern int AXUIElementPerformAction(nint element, nint action);

    [DllImport(ApplicationServices)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AXIsProcessTrusted();

    [DllImport(CoreFoundation)]
    private static extern nint CFStringCreateWithCString(
        nint allocator,
        string value,
        uint encoding);

    [DllImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFStringGetCString(
        nint value,
        byte[] buffer,
        nint bufferSize,
        uint encoding);

    [DllImport(CoreFoundation)]
    private static extern nint CFArrayGetCount(nint array);

    [DllImport(CoreFoundation)]
    private static extern nint CFArrayGetValueAtIndex(nint array, nint index);

    [DllImport(CoreFoundation)]
    private static extern nint CFRetain(nint value);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(nint value);
}