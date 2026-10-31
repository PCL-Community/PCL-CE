namespace PCL;

/// <summary>将业务层交互契约桥接到 WPF 提示与消息框。</summary>
public sealed class WpfUserInteraction : IUserInteraction
{
    public void ShowHint(string text, NotificationType type = NotificationType.Info)
    {
        var hintType = type switch
        {
            NotificationType.Success => NotificationType.Success,
            NotificationType.Warning => NotificationType.Warning,
            NotificationType.Error => NotificationType.Error,
            _ => NotificationType.Info
        };
        DialogCompat.ShowHint(text, hintType);
    }

    public void ShowMessageBox(
        string text,
        string? title = null,
        string? button1 = null,
        bool isWarn = false)
    {
        DialogCompat.ShowMessageBox(text, title, button1, isWarn: isWarn);
    }

    public void OpenWebsite(string url)
    {
        Common.OpenWebsite(url);
    }

    public bool ShowConfirm(
        string text,
        string? title = null,
        string? confirmText = null,
        string? cancelText = null)
    {
        return DialogCompat.ShowMessageBox(text, title, confirmText, cancelText ?? "") == 1;
    }
}