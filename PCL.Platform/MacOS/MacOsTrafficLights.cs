using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PCL.Platform;

[SupportedOSPlatform("macos")]
public static partial class MacOsTrafficLights
{
    private const string ObjCLibrary = "/usr/lib/libobjc.A.dylib";

    private static readonly nint _StandardWindowButton = _RegisterName("standardWindowButton:");
    private static readonly nint _Superview = _RegisterName("superview");
    private static readonly nint _Frame = _RegisterName("frame");
    private static readonly nint _SetFrame = _RegisterName("setFrame:");

    public static double LeadingWidth(double titleBarHeight)
    {
        return titleBarHeight + 30d;
    }

    public static void Layout(nint window, double titleBarHeight)
    {
        var close = _SendPtr(window, _StandardWindowButton, 0);
        if (close == 0) return;
        var titlebar = _SendPtr(close, _Superview);
        var container = titlebar == 0 ? 0 : _SendPtr(titlebar, _Superview);
        if (container == 0) return;

        var windowFrame = _GetFrame(window);
        _SetFrameIfChanged(
            container,
            _GetFrame(container) with { Y = windowFrame.Height - titleBarHeight, Height = titleBarHeight });
        _SetFrameIfChanged(titlebar, _GetFrame(titlebar) with { Y = 0d, Height = titleBarHeight });

        var closeFrame = _GetFrame(close);
        var margin = Math.Round((titleBarHeight - closeFrame.Height) / 2d);
        var offsetX = margin - closeFrame.X;
        for (nint kind = 0; kind < 3; kind++)
        {
            var button = _SendPtr(window, _StandardWindowButton, kind);
            if (button == 0) continue;

            var frame = _GetFrame(button);
            _SetFrameIfChanged(button, frame with { X = frame.X + offsetX, Y = margin });
        }
    }

    private static void _SetFrameIfChanged(nint view, NativeRect frame)
    {
        var current = _GetFrame(view);
        if (Math.Abs(current.X - frame.X) < 0.01d
            && Math.Abs(current.Y - frame.Y) < 0.01d
            && Math.Abs(current.Height - frame.Height) < 0.01d)
            return;

        _SendSetFrame(view, _SetFrame, frame);
    }

    private static NativeRect _GetFrame(nint view)
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64) return _SendGetFrame(view, _Frame);

        _SendGetFrameStret(out var frame, view, _Frame);
        return frame;
    }

    [StructLayout(LayoutKind.Sequential)]
    private record struct NativeRect(double X, double Y, double Width, double Height);

    [LibraryImport(ObjCLibrary, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint _RegisterName(string name);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    private static partial nint _SendPtr(nint receiver, nint selector);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    private static partial nint _SendPtr(nint receiver, nint selector, nint argument);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    private static partial NativeRect _SendGetFrame(nint receiver, nint selector);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend_stret")]
    private static partial void _SendGetFrameStret(out NativeRect frame, nint receiver, nint selector);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    private static partial void _SendSetFrame(nint receiver, nint selector, NativeRect frame);
}
