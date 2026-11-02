using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.UI.Dialogs;

namespace PCL.UI;

/// <summary>把 Core 的弹窗状态渲染成界面窗口。</summary>
public sealed class DialogPresenter : IDialogPresenter
{
    public static DialogPresenter Instance { get; } = new();

    private readonly Dictionary<DialogViewModel, Window> _windows = new();

    public static void Install()
    {
        DialogService.Instance.Presenter = Instance;
    }

    public bool IsAvailable
    {
        get
        {
            var window = _GetMainWindow();
            return window is { IsVisible: true } && window.WindowState != WindowState.Minimized;
        }
    }

    public void Present(DialogViewModel dialog)
    {
        var owner = _GetMainWindow();
        if (owner is null)
        {
            ShowFallback(dialog);
            return;
        }

        try
        {
            var window = _BuildWindow(dialog);
            _windows[dialog] = window;
            window.Closed += (_, _) => _OnWindowClosed(dialog);
            dialog.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DialogViewModel.IsClosed) && dialog.IsClosed)
                    Dispatcher.UIThread.Post(() => _CloseWindow(dialog));
            };
            window.Show(owner);
            window.Activate();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[UI] 弹窗显示失败：{ex}");
            dialog.Close(DialogResult.None);
        }
    }

    public void Dismiss(DialogViewModel dialog)
    {
        _CloseWindow(dialog);
    }

    public void ShowFallback(DialogViewModel dialog)
    {
        if (Application.Current is null)
        {
            Console.Error.WriteLine($"[UI] [{dialog.Title}] {dialog.TextContent}");
            dialog.Close(DialogResult.None);
            return;
        }

        try
        {
            var window = _BuildWindow(dialog);
            _windows[dialog] = window;
            window.Closed += (_, _) => _OnWindowClosed(dialog);
            dialog.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DialogViewModel.IsClosed) && dialog.IsClosed)
                    Dispatcher.UIThread.Post(() => _CloseWindow(dialog));
            };
            window.Show();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[UI] 弹窗兜底显示失败：{ex}");
            dialog.Close(DialogResult.None);
        }
    }

    private void _OnWindowClosed(DialogViewModel dialog)
    {
        _windows.Remove(dialog);
        if (dialog.IsClosed) return;

        // 用户直接关闭窗口时视为取消
        dialog.Close(dialog.Buttons.Length > 1 ? DialogResult.Cancel : DialogResult.None);
    }

    private void _CloseWindow(DialogViewModel dialog)
    {
        if (!_windows.Remove(dialog, out var window)) return;
        if (!window.IsVisible) return;
        try
        {
            window.Close();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[UI] 关闭弹窗失败：{ex}");
        }
    }

    private static Window _BuildWindow(DialogViewModel dialog)
    {
        var window = new Window
        {
            Title = dialog.Title,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 640,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var title = new SelectableTextBlock
        {
            Text = dialog.Title,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var body = _BuildBody(dialog);
        var buttons = _BuildButtons(dialog, window);

        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(20)
        };
        panel.Children.Add(title);
        panel.Children.Add(body);
        panel.Children.Add(buttons);
        window.Content = panel;

        window.Opened += (_, _) => _FocusFirstButtonOrInput(window);
        return window;
    }

    private static Control _BuildBody(DialogViewModel dialog)
    {
        switch (dialog.Kind)
        {
            case DialogKind.Input:
            {
                var input = new TextBox
                {
                    Text = dialog.InputDefaultValue,
                    PlaceholderText = string.IsNullOrEmpty(dialog.HintText) ? null : dialog.HintText
                };
                input.TextChanged += (_, _) => dialog.InputText = input.Text ?? "";
                dialog.InputText = dialog.InputDefaultValue;
                return input;
            }
            case DialogKind.Select:
            {
                var list = new StackPanel
                {
                    Spacing = 4
                };
                for (var i = 0; i < dialog.Options.Count; i++)
                {
                    var option = dialog.Options[i];
                    if (dialog.MultiSelect)
                    {
                        var optionIndex = i;
                        var check = new CheckBox
                        {
                            Content = option.Text,
                            IsChecked = option.IsChecked
                        };
                        check.IsCheckedChanged += (_, _) =>
                            dialog.Options[optionIndex].IsChecked = check.IsChecked == true;
                        list.Children.Add(check);
                    }
                    else
                    {
                        var index = i;
                        var radio = new RadioButton
                        {
                            Content = option.Text,
                            GroupName = "PCLDialogOption",
                            IsChecked = dialog.SelectedIndex == i
                        };
                        radio.IsCheckedChanged += (_, _) =>
                        {
                            if (radio.IsChecked == true) dialog.Select(index);
                        };
                        list.Children.Add(radio);
                    }
                }

                return new ScrollViewer
                {
                    Content = list,
                    MaxHeight = 320
                };
            }
            case DialogKind.Text:
            case DialogKind.Markdown:
            case DialogKind.AuthServer:
            case DialogKind.Login:
            default:
            {
                var text = string.IsNullOrEmpty(dialog.TextContent)
                    ? dialog.Title
                    : dialog.TextContent;
                return new ScrollViewer
                {
                    MaxHeight = 400,
                    Content = new SelectableTextBlock
                    {
                        Text = text,
                        TextWrapping = TextWrapping.Wrap
                    }
                };
            }
        }
    }

    private static Control _BuildButtons(DialogViewModel dialog, Window window)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0)
        };

        for (var i = 1; i <= dialog.Buttons.Length; i++)
        {
            var buttonIndex = i;
            var button = new Button
            {
                Content = dialog.ButtonText(buttonIndex),
                MinWidth = 88
            };
            button.Click += (_, _) =>
            {
                if (dialog.IsClosed) return;

                var action = dialog.ButtonAction(buttonIndex);
                if (action is not null)
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[UI] 弹窗按钮动作执行失败：{ex}");
                    }

                    return;
                }

                dialog.CloseByButton(buttonIndex);
                window.Close();
            };
            panel.Children.Add(button);
        }

        return panel;
    }

    private static void _FocusFirstButtonOrInput(Window window)
    {
        if (window.Content is not StackPanel panel) return;
        foreach (var child in panel.Children)
            switch (child)
            {
                case TextBox textBox:
                    textBox.Focus();
                    textBox.SelectAll();
                    return;
                case StackPanel buttonPanel:
                    foreach (var button in buttonPanel.Children)
                    {
                        if (button is not Button { Content: not null } firstButton) continue;
                        firstButton.Focus();
                        return;
                    }

                    return;
            }
    }

    private static Window? _GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            lifetime)
            return lifetime.MainWindow;
        return null;
    }
}