namespace PCL.Core.UI;

public sealed class MacOsNotificationService : NotificationServiceBase
{
    protected override bool TrySend(string title, string message)
    {
        var script = $"display notification \"{Escape(message)}\" with title \"{Escape(title)}\"";
        return Run("osascript", ["-e", script]);
    }

    private static string Escape(string text)
    {
        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}