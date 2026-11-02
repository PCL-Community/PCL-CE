using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>基于 Avalonia 窗口的致命错误对话框实现。</summary>
public sealed class FatalDialogService : IFatalDialogService
{
    public void ShowFatal(string message, string caption)
    {
        try
        {
            if (Application.Current is null)
            {
                Console.Error.WriteLine($"[{caption}] {message}");
                return;
            }

            if (Dispatcher.UIThread.CheckAccess())
                _Show(message, caption);
            else
                Dispatcher.UIThread.Post(() => _Show(message, caption));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{caption}] {message}{Environment.NewLine}{ex}");
        }
    }

    private static void _Show(string message, string caption)
    {
        var window = new Window
        {
            Title = caption,
            Width = 640,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        var text = new SelectableTextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(16)
        };

        var button = new Button
        {
            Content = Lang.Text("Common.Action.Confirm"),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16),
            MinWidth = 96
        };
        button.Click += (_, _) => window.Close();

        var panel = new DockPanel();
        DockPanel.SetDock(button, Dock.Bottom);
        panel.Children.Add(button);
        panel.Children.Add(
            new ScrollViewer
            {
                Content = text
            });

        window.Content = panel;
        window.Show();
    }
}