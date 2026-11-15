namespace PCL.Core.Platform;

/// <summary>系统通知。具体实现由组合根注册到 <see cref="ApplicationRuntime" />。</summary>
public interface ISystemNotificationService
{
    /// <summary>当前是否可以发送通知。</summary>
    bool IsAvailable { get; }

    /// <summary>发送一条通知。</summary>
    void Show(string title, string message);
}