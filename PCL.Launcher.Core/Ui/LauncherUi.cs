namespace PCL;

/// <summary>业务层提示类型。</summary>
public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>业务层与用户的直接交互（提示、消息框）。</summary>
public interface IUserInteraction
{
    void ShowHint(string text, NotificationType type = NotificationType.Info);

    void ShowMessageBox(
        string text,
        string? title = null,
        string? button1 = null,
        bool isWarn = false);

    void OpenWebsite(string url);

    bool ShowConfirm(
        string text,
        string? title = null,
        string? confirmText = null,
        string? cancelText = null);
}

/// <summary>UI 层注册的交互实现。</summary>
public static class LauncherUi
{
    public static IUserInteraction? Interaction { get; set; }
}