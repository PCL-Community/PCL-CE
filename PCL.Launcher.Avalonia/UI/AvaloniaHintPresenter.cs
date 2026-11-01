using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.UI.Hints;

namespace PCL.Avalonia.UI;

/// <summary>把 Core 的提示显示为主窗口右下角的浮层。</summary>
public sealed class AvaloniaHintPresenter : IHintPresenter
{
    public static AvaloniaHintPresenter Instance { get; } = new();

    private readonly Dictionary<HintViewModel, Window> _windows = new();

    public static void Install()
    {
        HintService.Instance.Presenter = Instance;
    }

    public bool IsAvailable
    {
        get
        {
            var window = _GetMainWindow();
            return window is { IsVisible: true };
        }
    }

    public void Present(HintViewModel hint)
    {
        var owner = _GetMainWindow();
        if (owner is null)
        {
            hint.Close();
            return;
        }

        var window = new Window
        {
            WindowDecorations = WindowDecorations.None,
            CanResize = false,
            ShowInTaskbar = false,
            Topmost = true,
            Background = Brushes.Transparent,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content = _BuildContent(hint)
        };

        _windows[hint] = window;
        hint.DismissRequested += _OnDismissRequested;
        hint.EmphasizeRequested += _OnEmphasizeRequested;
        hint.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HintViewModel.IsClosed) && hint.IsClosed)
                Dispatcher.UIThread.Post(() => _CloseWindow(hint));
        };

        window.Opened += (_, _) => _PositionWindow(window, owner, hint);
        window.Show(owner);

        DispatcherTimer.RunOnce(
            () =>
            {
                if (hint.IsClosed || hint.IsDismissing) return;
                hint.Dismiss();
            },
            TimeSpan.FromMilliseconds(hint.DisplayDuration));
    }

    public void Dismiss(HintViewModel hint)
    {
        _CloseWindow(hint);
        hint.Close();
    }

    public void DismissAll()
    {
        foreach (var hint in _windows.Keys.ToArray())
            Dismiss(hint);
    }

    private void _OnDismissRequested(HintViewModel hint)
    {
        Dismiss(hint);
    }

    private void _OnEmphasizeRequested(HintViewModel hint)
    {
        if (!_windows.TryGetValue(hint, out var window)) return;
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void _CloseWindow(HintViewModel hint)
    {
        if (!_windows.Remove(hint, out var window)) return;
        hint.DismissRequested -= _OnDismissRequested;
        hint.EmphasizeRequested -= _OnEmphasizeRequested;
        if (!window.IsVisible) return;
        try
        {
            window.Close();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Avalonia] 关闭提示失败：{ex}");
        }
    }

    private static Control _BuildContent(HintViewModel hint)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10),
            Margin = new Thickness(8),
            Background = new SolidColorBrush(_GetBackgroundColor(hint.Kind)),
            Child = new TextBlock
            {
                Text = hint.Message,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420
            }
        };
        return border;
    }

    private static Color _GetBackgroundColor(HintKind kind)
    {
        return kind switch
        {
            HintKind.Success => Color.FromArgb(0xEE, 0x2E, 0x7D, 0x32),
            HintKind.Error => Color.FromArgb(0xEE, 0xB7, 0x1C, 0x1C),
            HintKind.Warning => Color.FromArgb(0xEE, 0xB2, 0x6A, 0x00),
            _ => Color.FromArgb(0xEE, 0x32, 0x32, 0x32)
        };
    }

    private void _PositionWindow(Window window, Window owner, HintViewModel hint)
    {
        var screen = owner.Screens.ScreenFromWindow(owner) ?? owner.Screens.Primary;
        if (screen is null) return;

        var scaling = screen.Scaling;
        var width = (int)(window.Bounds.Width * scaling);
        var height = (int)(window.Bounds.Height * scaling);
        var workArea = screen.WorkingArea;
        var offset = _windows.Keys.ToList().IndexOf(hint);
        if (offset < 0) offset = 0;

        var x = workArea.Right - width - 24;
        var y = workArea.Bottom - height - 24 - offset * (height + 8);
        window.Position = new PixelPoint(x, y);
    }

    private static Window? _GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            lifetime)
            return lifetime.MainWindow;
        return null;
    }
}