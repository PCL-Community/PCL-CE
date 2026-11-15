using Avalonia;
using PCL.Core.Logging;

namespace PCL.Core.UI;

public sealed class LinuxNotificationService : NotificationServiceBase
{
    private const string AppName = "PCL Community Edition";
    private const int ExpireTimeMs = 5000;

    public override bool IsAvailable => HasSessionBus || Application.Current is not null;

    private static bool HasSessionBus
    {
        get
        {
            if (!string.IsNullOrEmpty(
                    Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")))
                return true;
            var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            return !string.IsNullOrEmpty(runtimeDir)
                && File.Exists(Path.Combine(runtimeDir, "bus"));
        }
    }

    protected override bool TrySend(string title, string message)
    {
        if (!HasSessionBus)
        {
            LogWrapper.Debug("UI", "Linux 通知不可用：没有会话 D-Bus");
            return false;
        }

        if (Run(
                "dbus-send",
                [
                    "--session", "--type=method_call", "--dest=org.freedesktop.Notifications",
                    "/org/freedesktop/Notifications", "org.freedesktop.Notifications.Notify",
                    $"string:{AppName}", "uint32:0", "string:", $"string:{title}",
                    $"string:{message}", "array:string:", "dict:string:variant:",
                    $"int32:{ExpireTimeMs}"
                ]))
            return true;

        return Run(
            "notify-send",
            ["--app-name", AppName, "--expire-time", ExpireTimeMs.ToString(), title, message]);
    }
}