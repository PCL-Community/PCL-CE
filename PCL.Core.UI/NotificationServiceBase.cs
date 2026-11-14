using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>平台通知共享的后台发送与应用内 fallback。</summary>
public abstract class NotificationServiceBase : ISystemNotificationService
{
    public virtual bool IsAvailable => Application.Current is not null;

    public void Show(string title, string message)
    {
        _ = Task.Run(() =>
        {
            if (TrySend(title, message)) return;
            if (Application.Current is null) return;
            Dispatcher.UIThread.Post(() => ShowFallback(title, message));
        });
    }

    protected abstract bool TrySend(string title, string message);

    protected static bool Run(string fileName, IReadOnlyList<string> arguments)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            using var process = Process.Start(info);
            if (process is null) return false;
            if (process.WaitForExit(3000)) return process.ExitCode == 0;
            process.Kill(true);
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static void ShowFallback(string title, string message)
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
            window.Opened += (_, _) => Position(window);
            window.Show();
            DispatcherTimer.RunOnce(window.Close, TimeSpan.FromSeconds(5));
        }
        catch
        {
            // 通知失败不应影响主流程
        }
    }

    private static void Position(Window window)
    {
        var screen = window.Screens.Primary;
        if (screen is null) return;
        var area = screen.WorkingArea;
        window.Position = new PixelPoint(
            area.X + area.Width - (int)window.Bounds.Width - 16,
            area.Y + area.Height - (int)window.Bounds.Height - 16);
    }
}