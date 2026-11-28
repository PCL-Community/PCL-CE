using System.Runtime.InteropServices;
using PCL.Core.Logging;
using PlainToolkit.ObjectiveC.Runtime;

namespace PCL;

/// <summary>在 AppKit 完成窗口缩放后同步回调；创建、回调和释放均在 UI 线程。</summary>
internal sealed class MacOsNativeResizeObserver : IDisposable
{
    private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";
    private static readonly ObjCSel Alloc = ObjCSel.FromString("alloc");
    private static readonly ObjCSel Init = ObjCSel.FromString("init");
    private static readonly ObjCSel Release = ObjCSel.FromString("release");
    private static readonly ObjCSel DefaultCenter = ObjCSel.FromString("defaultCenter");
    private static readonly ObjCSel AddObserver = ObjCSel.FromString("addObserver:selector:name:object:");
    private static readonly ObjCSel RemoveObserver = ObjCSel.FromString("removeObserver:");
    private static readonly ObjCSel ReceiveResize = ObjCSel.FromString("pcl_nativeWindowDidResize:");

    // Runtime 持有 IMP，delegate 也必须在进程生命周期内保持存活。
    private static readonly NotificationHandler NativeHandler = OnNativeResize;
    private static readonly Lazy<ObjCObject> ObserverClass = new(CreateObserverClass);
    private static readonly Dictionary<nint, Action> Callbacks = [];

    private readonly ObjCObject _center = ObjCObject.GetClass("NSNotificationCenter")
        .SendMessage<ObjCObject>(DefaultCenter);
    private ObjCObject _observer;

    public MacOsNativeResizeObserver(ObjCObject window, Action callback)
    {
        var name = ObjCString.FromString("NSWindowDidResizeNotification");
        try
        {
            _observer = ObserverClass.Value.SendMessage<ObjCObject>(Alloc).SendMessage<ObjCObject>(Init);
            if (_observer.IsNull) throw new InvalidOperationException("Could not create the resize observer.");
            Callbacks.Add(_observer.Value, callback);
            _center.SendMessage(AddObserver, _observer, ReceiveResize, name.Value, window);
        }
        catch
        {
            Dispose();
            throw;
        }
        finally
        {
            new ObjCObject(name.Value).SendMessage(Release);
        }
    }

    public void Dispose()
    {
        if (_observer.IsNull) return;
        var observer = _observer;
        _observer = default;
        Callbacks.Remove(observer.Value);
        try
        {
            _center.SendMessage(RemoveObserver, observer);
        }
        finally
        {
            observer.SendMessage(Release);
        }
    }

    private static ObjCObject CreateObserverClass()
    {
        var nativeClass = objc_allocateClassPair(ObjCObject.GetClass("NSObject").Value,
            "PCLCaptionResizeObserver_" + Guid.NewGuid().ToString("N"), 0);
        if (nativeClass == 0) throw new InvalidOperationException("Could not allocate the resize observer class.");
        if (!class_addMethod(nativeClass, ReceiveResize.Value, Marshal.GetFunctionPointerForDelegate(NativeHandler), "v@:@"))
        {
            objc_disposeClassPair(nativeClass);
            throw new InvalidOperationException("Could not add the resize notification selector.");
        }

        objc_registerClassPair(nativeClass);
        return new ObjCObject(nativeClass);
    }

    private static void OnNativeResize(nint self, nint selector, nint notification)
    {
        try
        {
            if (Callbacks.TryGetValue(self, out var callback)) callback();
        }
        catch (Exception error)
        {
            // 托管异常（包括日志异常）不能越过原生回调边界。
            try { LogWrapper.Error(error, "UI", "处理 macOS 原生窗口缩放通知失败"); }
            catch { }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NotificationHandler(nint self, nint selector, nint notification);

    [DllImport(ObjectiveCLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint objc_allocateClassPair(nint superclass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, nuint extraBytes);

    [DllImport(ObjectiveCLibrary, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool class_addMethod(nint nativeClass, nint selector, nint implementation, [MarshalAs(UnmanagedType.LPUTF8Str)] string types);

    [DllImport(ObjectiveCLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern void objc_registerClassPair(nint nativeClass);

    [DllImport(ObjectiveCLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern void objc_disposeClassPair(nint nativeClass);
}
