using System;

namespace PCL.Core.Utils.WinRT.Interface.Windows.UI.Notifications;

public static class IToastNotificationManagerStaticsInfo
{
    public static readonly string ActivatableClassId = "Windows.UI.Notifications.ToastNotificationManager";
    public static readonly Guid Iid = new("50ac103f-d235-4598-bbef-98fe4d1a3ad4");
}

public unsafe struct IToastNotificationManagerStatics
{
    public IToastNotificationManagerStaticsVtbl* lpVtbl;
}

public unsafe struct IToastNotificationManagerStaticsVtbl
{
    // IUnknown
    public delegate* unmanaged<void*, Guid*, void**, int> QueryInterface;
    public delegate* unmanaged<void*, uint> AddRef;
    public delegate* unmanaged<void*, uint> Release;

    // IInspectable
    public delegate* unmanaged<void*, uint*, Guid**, int> GetIids;
    public delegate* unmanaged<void*, IntPtr*, int> GetRuntimeClassName;
    public delegate* unmanaged<void*, int*, int> GetTrustLevel;

    // IToastNotificationManagerStatics
    
    /// <summary>
    /// ToastNotifier CreateToastNotifier()
    /// </summary>
    public delegate* unmanaged<void*, void**, int> CreateToastNotifier;
    
    /// <summary>
    /// ToastNotifier CreateToastNotifier(string applicationId)
    /// </summary>
    public delegate* unmanaged<void*, IntPtr, void**, int> CreateToastNotifierWithId;

    /// <summary>
    /// XmlDocument GetTemplateContent(ToastTemplateType type)
    /// </summary>
    public delegate* unmanaged<void*, int, void**, int> GetTemplateContent;
}