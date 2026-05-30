using System;

namespace PCL.Core.Utils.WinRT.Interface.Windows.UI.Notifications;

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