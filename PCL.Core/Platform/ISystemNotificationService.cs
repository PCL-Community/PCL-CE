namespace PCL.Core.Platform;

/// <summary>
///     系统通知。具体实现由 UI 层注册到 <see cref="PlatformServices.Notification" />。
/// </summary>
public interface ISystemNotificationService
{
    /// <summary>当前是否可以发送通知。</summary>
    bool IsAvailable { get; }

    /// <summary>发送一条通知。</summary>
    void Show(string title, string message);
}