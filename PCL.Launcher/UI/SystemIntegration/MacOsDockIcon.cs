using System.Runtime.InteropServices;
using Avalonia.Platform;

namespace PCL.UI.SystemIntegration;

internal static class MacOsDockIcon
{
    public static void Apply()
    {
        using var stream = AssetLoader.Open(
            new Uri("avares://PCL.Launcher/Assets/Images/icon-mac.png"));
        var icon = new byte[stream.Length];
        stream.ReadExactly(icon);

        var pinned = GCHandle.Alloc(icon, GCHandleType.Pinned);
        try
        {
            var data = Send(
                Send(objc_getClass("NSData"), sel_registerName("alloc")),
                sel_registerName("initWithBytes:length:"),
                pinned.AddrOfPinnedObject(),
                icon.Length);
            var image = Send(
                Send(objc_getClass("NSImage"), sel_registerName("alloc")),
                sel_registerName("initWithData:"),
                data);
            Send(data, sel_registerName("release"));
            Send(
                Send(objc_getClass("NSApplication"), sel_registerName("sharedApplication")),
                sel_registerName("setApplicationIconImage:"),
                image);
            Send(image, sel_registerName("release"));
        }
        finally
        {
            pinned.Free();
        }
    }

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern nint objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern nint sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector, nint arg1);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector, nint arg1, nint arg2);
}
