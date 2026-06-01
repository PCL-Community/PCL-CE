using System;
using System.Runtime.InteropServices;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.WinRT;
using PCL.Core.Utils.WinRT.Interface;
using PCL.Core.Utils.WinRT.Interface.Windows.Data.Xml.Dom;
using PCL.Core.Utils.WinRT.Interface.Windows.UI.Notifications;

namespace PCL.Core.UI;

public static class ToastNotification
{
    /// <summary>
    /// Send a Toast notification with simple texts to the system.
    /// </summary>
    /// <param name="message">Notification detail text</param>
    /// <param name="title">Notification title</param>
    public static unsafe void SendToast(string message, string title = "Notice")
    {
        // var toast = new ToastContentBuilder();
        // toast
        //     .AddArgument("action", "viewConversation")
        //     .AddText(title)
        //     .AddText(message);
        //
        // toast.Show();
        // TODO
        
        var xml = HStringHelper.ToHString($"""
            <toast>
                <visual>
                    <binding template="ToastGeneric">
                        <text>{title}</text>
                        <text>{message}</text>
                    </binding>
                </visual>
            </toast>
            """);

        if (!AumidHelper.HasAumid())
        {
            AumidHelper.RegisterAumid("PCLCommunity.PCLCE");
        }
        var aumid = HStringHelper.ToHString("PCLCommunity.PCLCE");
        
        fixed (Guid* xmlDocumentIOIid = &IXmlDocumentIOInfo.Iid)
        {
            void* xmlDocumentIO;
            void* toastNotification;
            void* toastNotifier;

            var inspectable = (IInspectable*)WinRTInterop.ActivateInstance(IXmlDocumentIOInfo.ActivatableClassId);
            Marshal.ThrowExceptionForHR(
                inspectable->lpVtbl->QueryInterface(inspectable, xmlDocumentIOIid, &xmlDocumentIO));
            Marshal.ThrowExceptionForHR(
                ((IXmlDocumentIO*)xmlDocumentIO)->lpVtbl->LoadXml(xmlDocumentIO, xml));

            var toastNotificationFactory =
                (IToastNotificationFactory*)WinRTInterop.GetActivationFactory(
                    IToastNotificationFactoryInfo.ActivatableClassId, IToastNotificationFactoryInfo.Iid);
            Marshal.ThrowExceptionForHR(
                toastNotificationFactory->lpVtbl->CreateToastNotification(toastNotificationFactory, xmlDocumentIO,
                    &toastNotification));

            var toastNotificationManagerStatics = (IToastNotificationManagerStatics*)WinRTInterop.GetActivationFactory(
                IToastNotificationManagerStaticsInfo.ActivatableClassId, IToastNotificationManagerStaticsInfo.Iid);
            Marshal.ThrowExceptionForHR(
                toastNotificationManagerStatics->lpVtbl->CreateToastNotifierWithId(toastNotificationManagerStatics,
                    aumid, &toastNotifier));
            Marshal.ThrowExceptionForHR(
                ((IToastNotifier*)toastNotifier)->lpVtbl->Show(toastNotifier, toastNotification));
        }
        
        HStringHelper.DeleteHString(xml);
        HStringHelper.DeleteHString(aumid);
    }

    /// <summary>
    /// Remove Toast notifications and related cache from the system.
    /// </summary>
    public static void UninstallToasts()
    {
        // ToastNotificationManagerCompat.Uninstall();
        // TODO
    }
}