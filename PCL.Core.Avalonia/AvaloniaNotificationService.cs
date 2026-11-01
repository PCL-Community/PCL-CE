using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.Platform;

namespace PCL.Core.Avalonia;

/// <summary>
///     Linux / macOS 系统通知：通过 freedesktop 的 <c>org.freedesktop.Notifications</c> 服务
///     （GNOME、KDE Plasma 等桌面环境均实现）发送；通知服务不可用时退回应用内浮层。
/// </summary>
public sealed class AvaloniaNotificationService : ISystemNotificationService
{
    private const string AppName = "PCL Community Edition";
    private const int ExpireTimeMs = 5000;

    public bool IsAvailable => HasSessionBus || Application.Current is not null;

    public void Show(string title, string message)
    {
        if (OperatingSystem.IsWindows()) return;

        if (_TrySendDesktopNotification(title, message)) return;

        if (Application.Current is null) return;
        Dispatcher.UIThread.Post(() => _ShowFallbackToast(title, message));
    }

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

    // TODO: macOS 没有 DBus，当前会退化到应用内浮层；应改为 osascript display notification。
    private static bool _TrySendDesktopNotification(string title, string message)
    {
        if (!HasSessionBus) return false;

        // dbus-send 与 notify-send 都直接走 org.freedesktop.Notifications，参数按 argv 传递无需转义
        if (_TryRun(
                "dbus-send",
                [
                    "--session",
                    "--type=method_call",
                    "--dest=org.freedesktop.Notifications",
                    "/org/freedesktop/Notifications",
                    "org.freedesktop.Notifications.Notify",
                    $"string:{AppName}",
                    "uint32:0",
                    "string:",
                    $"string:{title}",
                    $"string:{message}",
                    "array:string:",
                    "dict:string:variant:",
                    $"int32:{ExpireTimeMs}"
                ]))
            return true;

        return _TryRun(
            "notify-send",
            ["--app-name", AppName, "--expire-time", ExpireTimeMs.ToString(), title, message]);
    }

    private static bool _TryRun(string fileName, IReadOnlyList<string> arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process is null) return false;
            
            if (process.WaitForExit(3000))
                return process.ExitCode == 0;
            
            process.Kill(true);
            return false;
        }
        catch (Exception)
        {
            // 命令不存在时继续尝试下一种实现
            return false;
        }
    }

    /// <summary>没有可用通知服务时的兜底：屏幕右下角置顶浮层。</summary>
    private static void _ShowFallbackToast(string title, string message)
    {
        try
        {
            var window = new Window
            {
                WindowDecorations = WindowDecorations.None,
                CanResize = false,
                ShowInTaskbar = false,
                Topmost = true,
                Background = Brushes.Transparent,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Content = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 12),
                    Margin = new Thickness(8),
                    MaxWidth = 420,
                    Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x2B, 0x2B, 0x2B)),
                    Child = new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = title,
                                FontWeight = FontWeight.SemiBold,
                                Foreground = Brushes.White,
                                TextWrapping = TextWrapping.Wrap
                            },
                            new TextBlock
                            {
                                Text = message,
                                Foreground = new SolidColorBrush(
                                    Color.FromArgb(0xE0, 0xFF, 0xFF, 0xFF)),
                                TextWrapping = TextWrapping.Wrap
                            }
                        }
                    }
                }
            };

            window.Opened += (_, _) => _PositionWindow(window);
            window.Show();
            DispatcherTimer.RunOnce(
                () =>
                {
                    if (window.IsVisible) window.Close();
                },
                TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Avalonia] 显示通知失败：{ex}");
        }
    }

    private static void _PositionWindow(Window window)
    {
        var screen = window.Screens.Primary;
        if (screen is null) return;

        var scaling = screen.Scaling;
        var width = (int)(window.Bounds.Width * scaling);
        var height = (int)(window.Bounds.Height * scaling);
        var workArea = screen.WorkingArea;
        window.Position = new PixelPoint(
            workArea.Right - width - 24,
            workArea.Bottom - height - 24);
    }
}