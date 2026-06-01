using System;

namespace PCL.Core.Utils.WinRT.Interface.Windows.UI.Notifications;

public static class IToastNotificationFactoryInfo
{
    public static readonly string ActivatableClassId = "Windows.UI.Notifications.ToastNotification";
    public static readonly Guid Iid = new("04124b20-82c6-4229-b109-fd9ed4662b53");
}

internal unsafe struct IToastNotificationFactory
{
    public IToastNotificationFactoryVtbl* lpVtbl;
}

internal unsafe struct IToastNotificationFactoryVtbl
{
    // IUnknown
    public delegate* unmanaged<void*, Guid*, void**, int> QueryInterface;
    public delegate* unmanaged<void*, uint> AddRef;
    public delegate* unmanaged<void*, uint> Release;

    // IInspectable
    public delegate* unmanaged<void*, uint*, Guid**, int> GetIids;
    public delegate* unmanaged<void*, IntPtr*, int> GetRuntimeClassName;
    public delegate* unmanaged<void*, int*, int> GetTrustLevel;

    // IToastNotificationFactory

    // CreateToastNotification(IXmlDocument content, IToastNotification value)
    public delegate* unmanaged<void*, int*, int**> LoadXml;
}