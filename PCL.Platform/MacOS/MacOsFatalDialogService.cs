using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using PlainToolkit.ObjectiveC.Runtime;

namespace PCL.Platform;

public sealed class MacOsFatalDialogService : FatalDialogServiceBase
{
    private static readonly ObjCSel Alloc = ObjCSel.FromString("alloc");
    private static readonly ObjCSel Init = ObjCSel.FromString("init");
    private static readonly ObjCSel Release = ObjCSel.FromString("release");
    private static readonly ObjCSel RunModal = ObjCSel.FromString("runModal");
    private static readonly ObjCSel SetMessageText = ObjCSel.FromString("setMessageText:");
    private static readonly ObjCSel SetInformativeText = ObjCSel.FromString("setInformativeText:");
    private static readonly ObjCSel SetAlertStyle = ObjCSel.FromString("setAlertStyle:");
    private static readonly ObjCSel AddButtonWithTitle = ObjCSel.FromString("addButtonWithTitle:");
    private static readonly ObjCSel IsMainThreadSelector = ObjCSel.FromString("isMainThread");

    private static bool _frameworksLoaded;

    protected override bool ShowFatalCore(string message, string caption)
    {
        var displayMessage = TruncateForInlineDisplay(message);
        var shown = false;

        try
        {
            return TryInvokeOnMainThread(
                    () => shown = ShowAlert(caption, displayMessage),
                    TimeSpan.FromSeconds(10))
                && shown;
        }
        catch
        {
            return false;
        }
    }

    private static bool ShowAlert(string title, string message)
    {
        EnsureFrameworks();

        ObjCString? titleString = null;
        ObjCString? messageString = null;
        ObjCString? buttonString = null;
        var alert = default(ObjCObject);
        try
        {
            titleString = ObjCString.FromString(title);
            messageString = ObjCString.FromString(message);
            buttonString = ObjCString.FromString("OK");

            alert = ObjCObject
                .GetClass("NSAlert")
                .SendMessage<ObjCObject>(Alloc)
                .SendMessage<ObjCObject>(Init);
            if (alert.IsNull) return false;

            alert.SendMessage(SetMessageText, titleString.Value);
            alert.SendMessage(SetInformativeText, messageString.Value);
            alert.SendMessage(SetAlertStyle, (nint)2); // NSAlertStyleCritical
            alert.SendMessage(AddButtonWithTitle, buttonString.Value);

            _ = alert.SendMessage<nint>(RunModal);
            return true;
        }
        finally
        {
            if (!alert.IsNull) alert.SendMessage(Release);
            if (titleString is { } titleValue) ReleaseString(titleValue);
            if (messageString is { } messageValue) ReleaseString(messageValue);
            if (buttonString is { } buttonValue) ReleaseString(buttonValue);
        }
    }

    private static bool TryInvokeOnMainThread(Action action, TimeSpan timeout)
    {
        EnsureFrameworks();
        if (IsMainThread())
        {
            action();
            return true;
        }

        var work = new MainThreadWork(action);
        var handle = GCHandle.Alloc(work);
        try
        {
            unsafe
            {
                _DispatchAsync(
                    _GetMainQueue(),
                    GCHandle.ToIntPtr(handle),
                    &DispatchCallback);
            }
        }
        catch
        {
            handle.Free();
            return false;
        }

        if (!work.Done.Wait(timeout)) return false;
        if (work.Exception is not null)
            ExceptionDispatchInfo.Capture(work.Exception).Throw();

        return true;
    }

    private static bool IsMainThread()
    {
        return ObjCObject.GetClass("NSThread").SendMessage<bool>(IsMainThreadSelector);
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

    private static void ReleaseString(ObjCString value)
    {
        if (!value.IsNull)
            new ObjCObject(value.Value).SendMessage(Release);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void DispatchCallback(nint context)
    {
        var handle = GCHandle.FromIntPtr(context);
        var work = (MainThreadWork)handle.Target!;
        try
        {
            work.Action();
        }
        catch (Exception ex)
        {
            work.Exception = ex;
        }
        finally
        {
            work.Done.Set();
            handle.Free();
        }
    }

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dispatch_get_main_queue")]
    private static extern nint _GetMainQueue();

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dispatch_async_f")]
    private static unsafe extern void _DispatchAsync(
        nint queue,
        nint context,
        delegate* unmanaged[Cdecl]<nint, void> work);

    private sealed class MainThreadWork(Action action)
    {
        public Action Action { get; } = action;
        public Exception? Exception { get; set; }
        public ManualResetEventSlim Done { get; } = new();
    }
}