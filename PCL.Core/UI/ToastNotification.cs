namespace PCL.Core.UI;

public static class ToastNotification
{
    /// <summary>
    /// Send a Toast notification with simple texts to the system.
    /// </summary>
    /// <param name="message">Notification detail text</param>
    /// <param name="title">Notification title</param>
    public static void SendToast(string message, string title = "Notice")
    {
        // var toast = new ToastContentBuilder();
        // toast
        //     .AddArgument("action", "viewConversation")
        //     .AddText(title)
        //     .AddText(message);
        //
        // toast.Show();
        // TODO
    }

    /// <summary>
    /// Remove Toast notifications and related cache from the system.
    /// </summary>
    public static void UninstallToasts()
    {
        // ToastNotificationManagerCompat.Uninstall();
        // TODO
    }
    
    
}