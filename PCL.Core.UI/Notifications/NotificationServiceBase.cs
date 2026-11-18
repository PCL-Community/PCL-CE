using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>平台通知共享的后台发送与应用内 fallback。</summary>
public abstract class NotificationServiceBase(
    IExternalCommandRunner commandRunner)
    : ISystemNotificationService
{
    private readonly IExternalCommandRunner _commandRunner =
        commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));

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

    protected bool Run(string fileName, IReadOnlyList<string> arguments)
    {
        try
        {
            return _commandRunner
                .RunAsync(fileName, arguments, TimeSpan.FromSeconds(3))
                .GetAwaiter()
                .GetResult()
                .Succeeded;
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