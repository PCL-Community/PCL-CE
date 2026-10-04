using System;
using System.Runtime.InteropServices;
using System.Security;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.WinRT;
using PCL.Core.Utils.WinRT.Interface;
using PCL.Core.Utils.WinRT.Interface.Windows.Data.Xml.Dom;
using PCL.Core.Utils.WinRT.Interface.Windows.UI.Notifications;

namespace PCL.Core.UI;

public static class ToastNotification
{
    /// <summary>
    /// 发送简单的 Toast 通知。
    /// </summary>
    /// <param name="message">通知内容</param>
    /// <param name="title">通知标题</param>
    public static void SendToast(string message, string title = "Notice")
    {
        var xml = $"""
                   <toast>
                       <visual>
                           <binding template="ToastGeneric">
                               <text>{SecurityElement.Escape(title)}</text>
                               <text>{SecurityElement.Escape(message)}</text>
                           </binding>
                       </visual>
                   </toast>
                   """;

        SendToastFromTemplate(xml);
    }

    /// <summary>
    /// 根据模板发送 Toast 通知。
    /// </summary>
    /// <param name="xml">Toast 模板</param>
    public static unsafe void SendToastFromTemplate(string xml)
    {
        // 定义 Toast 模板
        var toastXml = HStringHelper.ToHString(xml);

        // TODO: 将 AUMID 判断放在其他位置
        if (!AumidHelper.HasAumid())
        {
            AumidHelper.RegisterAumid();
        }

        var aumid = HStringHelper.ToHString(AumidHelper.Aumid);

        // TODO: 支持触发事件

        IInspectable* inspectable = null;
        IXmlDocumentIO* xmlDocumentIO = null;
        IToastNotificationFactory* toastNotificationFactory = null;
        IToastNotificationManagerStatics* toastNotificationManagerStatics = null;
        IInspectable* toastNotification = null;
        IToastNotifier* toastNotifier = null;

        try
        {
            fixed (Guid* xmlDocumentIOIid = &IXmlDocumentIOInfo.Iid)
            {
                // 创建 XmlDocument 对象
                inspectable =
                    (IInspectable*)WinRTInterop.ActivateInstance(
                        IXmlDocumentIOInfo.ActivatableClassId);

                Marshal.ThrowExceptionForHR(
                    inspectable->lpVtbl->QueryInterface(
                        inspectable,
                        xmlDocumentIOIid,
                        (void**)&xmlDocumentIO));

                // 加载 XML
                Marshal.ThrowExceptionForHR(
                    xmlDocumentIO->lpVtbl->LoadXml(
                        xmlDocumentIO,
                        toastXml));

                // 获取 ToastNotificationFactory
                toastNotificationFactory =
                    (IToastNotificationFactory*)WinRTInterop.GetActivationFactory(
                        IToastNotificationFactoryInfo.ActivatableClassId,
                        IToastNotificationFactoryInfo.Iid);

                // 创建 ToastNotification
                Marshal.ThrowExceptionForHR(
                    toastNotificationFactory->lpVtbl->CreateToastNotification(
                        toastNotificationFactory,
                        xmlDocumentIO,
                        (void**)&toastNotification));

                // 获取 ToastNotificationManager
                toastNotificationManagerStatics =
                    (IToastNotificationManagerStatics*)WinRTInterop.GetActivationFactory(
                        IToastNotificationManagerStaticsInfo.ActivatableClassId,
                        IToastNotificationManagerStaticsInfo.Iid);

                // 创建 ToastNotifier
                Marshal.ThrowExceptionForHR(
                    toastNotificationManagerStatics->lpVtbl->CreateToastNotifierWithId(
                        toastNotificationManagerStatics,
                        aumid,
                        (void**)&toastNotifier));

                // 发送
                Marshal.ThrowExceptionForHR(
                    toastNotifier->lpVtbl->Show(
                        toastNotifier,
                        toastNotification));
            }
        }
        finally
        {
            if (toastNotifier is not null)
                toastNotifier->lpVtbl->Release(toastNotifier);

            if (toastNotification is not null)
                toastNotification->lpVtbl->Release(toastNotification);

            if (toastNotificationManagerStatics is not null)
                toastNotificationManagerStatics->lpVtbl->Release(
                    toastNotificationManagerStatics);

            if (toastNotificationFactory is not null)
                toastNotificationFactory->lpVtbl->Release(
                    toastNotificationFactory);

            if (xmlDocumentIO is not null)
                xmlDocumentIO->lpVtbl->Release(xmlDocumentIO);

            if (inspectable is not null)
                inspectable->lpVtbl->Release(inspectable);

            HStringHelper.DeleteHString(toastXml);
            HStringHelper.DeleteHString(aumid);
        }
    }

    /// <summary>
    /// Remove Toast notifications and related cache from the system.
    /// </summary>
    public static void UninstallToasts()
    {
        // TODO: 更改这里的逻辑
        AumidHelper.UnregisterAumid();
    }
}