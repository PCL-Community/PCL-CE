using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using PlainToolkit.ObjectiveC.Runtime;

namespace PCL;

internal interface IPlatformShellIntegration
{
    void OnFrameworkInitialized(Application application);
    void OnWindowCreated(Window window);
}

internal sealed class WindowsShellIntegration : IPlatformShellIntegration
{
    public void OnFrameworkInitialized(Application application)
    {
    }

    public void OnWindowCreated(Window window)
    {
    }
}

internal sealed class LinuxShellIntegration : IPlatformShellIntegration
{
    public void OnFrameworkInitialized(Application application)
    {
    }

    public void OnWindowCreated(Window window)
    {
    }
}

internal sealed class MacOsShellIntegration : IPlatformShellIntegration
{
    private static readonly ObjCSel Alloc = ObjCSel.FromString("alloc");

    private static readonly ObjCSel InitWithBytesLength =
        ObjCSel.FromString("initWithBytes:length:");

    private static readonly ObjCSel InitWithData = ObjCSel.FromString("initWithData:");
    private static readonly ObjCSel Release = ObjCSel.FromString("release");

    private static readonly ObjCSel SharedApplication =
        ObjCSel.FromString("sharedApplication");

    private static readonly ObjCSel SetApplicationIconImage =
        ObjCSel.FromString("setApplicationIconImage:");

    private static bool _frameworksLoaded;

    public void OnFrameworkInitialized(Application application)
    {
        ApplyDockIcon();
    }

    public void OnWindowCreated(Window window)
    {
    }

    private static void ApplyDockIcon()
    {
        EnsureFrameworks();

        using var stream = AssetLoader.Open(
            new Uri("avares://PCL.Launcher/Assets/Images/icon-mac.png"));
        var icon = new byte[stream.Length];
        stream.ReadExactly(icon);

        var pinned = GCHandle.Alloc(icon, GCHandleType.Pinned);
        var data = default(ObjCObject);
        var image = default(ObjCObject);
        try
        {
            data = ObjCObject
                .GetClass("NSData")
                .SendMessage<ObjCObject>(Alloc)
                .SendMessage<nint, nuint, ObjCObject>(
                    InitWithBytesLength,
                    pinned.AddrOfPinnedObject(),
                    (nuint)icon.Length);

            image = ObjCObject
                .GetClass("NSImage")
                .SendMessage<ObjCObject>(Alloc)
                .SendMessage<ObjCObject, ObjCObject>(InitWithData, data);

            data.SendMessage(Release);
            data = default;

            ObjCObject
                .GetClass("NSApplication")
                .SendMessage<ObjCObject>(SharedApplication)
                .SendMessage(SetApplicationIconImage, image);
        }
        finally
        {
            if (!data.IsNull) data.SendMessage(Release);
            if (!image.IsNull) image.SendMessage(Release);
            pinned.Free();
        }
    }

    private static void EnsureFrameworks()
    {
        if (_frameworksLoaded) return;

        NativeLibrary.Load(
            "/System/Library/Frameworks/Foundation.framework/Foundation");
        NativeLibrary.Load(
            "/System/Library/Frameworks/AppKit.framework/AppKit");
        _frameworksLoaded = true;
    }
}