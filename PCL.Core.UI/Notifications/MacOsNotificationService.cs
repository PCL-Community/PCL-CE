using System.Runtime.InteropServices;
using Avalonia.Threading;
using PCL.Core.Platform;
using PlainToolkit.ObjectiveC.Runtime;

namespace PCL.Core.UI;

public sealed class MacOsNotificationService(
    IExternalCommandRunner commandRunner)
    : NotificationServiceBase(commandRunner)
{
    private static bool _frameworksLoaded;

    protected override bool TrySend(string title, string message)
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
                return _Show(title, message);

            var shown = false;
            Dispatcher.UIThread.Invoke(() => shown = _Show(title, message));
            return shown;
        }
        catch
        {
            return false;
        }
    }

    private static bool _Show(string title, string message)
    {
        EnsureFrameworks();

        var notification = default(ObjCObject);
        ObjCString? titleString = null;
        ObjCString? messageString = null;
        try
        {
            var notificationClass = ObjCObject.GetClass("NSUserNotification");
            var centerClass = ObjCObject.GetClass("NSUserNotificationCenter");
            if (notificationClass.IsNull || centerClass.IsNull) return false;

            notification = notificationClass
                .SendMessage<ObjCObject>(ObjCSel.FromString("alloc"))
                .SendMessage<ObjCObject>(ObjCSel.FromString("init"));
            if (notification.IsNull) return false;

            titleString = ObjCString.FromString(title);
            messageString = ObjCString.FromString(message);
            notification.SendMessage(
                ObjCSel.FromString("setTitle:"),
                titleString.Value);
            notification.SendMessage(
                ObjCSel.FromString("setInformativeText:"),
                messageString.Value);

            var center = centerClass.SendMessage<ObjCObject>(
                ObjCSel.FromString("defaultUserNotificationCenter"));
            if (center.IsNull) return false;

            center.SendMessage(
                ObjCSel.FromString("deliverNotification:"),
                notification);
            return true;
        }
        finally
        {
            if (!notification.IsNull)
                notification.SendMessage(ObjCSel.FromString("release"));
            if (titleString is { } titleValue)
                new ObjCObject(titleValue.Value).SendMessage(ObjCSel.FromString("release"));
            if (messageString is { } messageValue)
                new ObjCObject(messageValue.Value).SendMessage(ObjCSel.FromString("release"));
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