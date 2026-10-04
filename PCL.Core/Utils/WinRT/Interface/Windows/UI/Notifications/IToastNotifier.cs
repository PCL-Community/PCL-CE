using System;

namespace PCL.Core.Utils.WinRT.Interface.Windows.UI.Notifications;

public static class IToastNotifierInfo
{
    public static readonly string ActivatableClassId = "Windows.UI.Notifications.ToastNotifier";
    public static readonly Guid Iid = new("75927b93-03f3-41ec-91d3-6e5bac1b38e7");
}

public unsafe struct IToastNotifier
{
    public IToastNotifierVtbl* lpVtbl;
}

public unsafe struct IToastNotifierVtbl
{
    // IUnknown
    public delegate* unmanaged<void*, Guid*, void**, int> QueryInterface;
    public delegate* unmanaged<void*, uint> AddRef;
    public delegate* unmanaged<void*, uint> Release;

    // IInspectable
    public delegate* unmanaged<void*, uint*, Guid**, int> GetIids;
    public delegate* unmanaged<void*, IntPtr*, int> GetRuntimeClassName;
    public delegate* unmanaged<void*, int*, int> GetTrustLevel;

    // IToastNotifier
    
    /// <summary>
    /// void Show(ToastNotification notification)
    /// </summary>
    public delegate* unmanaged<void*, void*, int> Show;

    /// <summary>
    /// void Hide(ToastNotification notification)
    /// </summary>
    public delegate* unmanaged<void*, void*, int> Hide;
    
    /// <summary>
    /// NotificationSetting Setting { get; }
    /// </summary>
    public delegate* unmanaged<void*, int, int> get_Setting;
    
    /// <summary>
    /// void AddToSchedule(ScheduledToastNotification scheduledToast)
    /// </summary>
    public delegate* unmanaged<void*, void*, int> AddToSchedule;
    
    /// <summary>
    /// void RemoveFromSchedule(ScheduledToastNotification scheduledToast)
    /// </summary>
    public delegate* unmanaged<void*, void*, int> RemoveFromSchedule;
    
    /// <summary>
    /// IVectorView&lt;ScheduledToastNotification&gt; GetScheduledToastNotifications()
    /// </summary>
    public delegate* unmanaged<void*, void**, int> GetScheduledToastNotifications;
}