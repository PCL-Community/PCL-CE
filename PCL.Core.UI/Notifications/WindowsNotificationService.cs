using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.UI;

public sealed class WindowsNotificationService(
    IExternalCommandRunner commandRunner)
    : NotificationServiceBase(commandRunner)
{
    protected override bool TrySend(string title, string message)
    {
        try
        {
            WindowsToastNotification.SendToast(message, title);
            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("UI", $"Windows Toast 通知发送失败，改用应用内提示：{ex.Message}");
            return false;
        }
    }
}