using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.Views;

namespace PCL.UI.SystemIntegration;

/// <summary>
///     macOS 原生菜单栏。Application 菜单承载 About/Preferences/Quit，
///     主窗口承载 File/Edit/Window/Help；语言切换时整体重建。
/// </summary>
public static class MacOsNativeMenu
{
    private static Application? _application;
    private static Window? _window;
    private static bool _installed;

    public static void Install(Application application)
    {
        if (_installed) return;
        _installed = true;
        _application = application;
        LocalizationService.LanguageChanged += RebuildOnUi;
        Rebuild();
    }

    public static void AttachWindow(Window window)
    {
        _window = window;
        Rebuild();
    }

    private static void RebuildOnUi()
    {
        if (Dispatcher.UIThread.CheckAccess())
            Rebuild();
        else
            Dispatcher.UIThread.Post(Rebuild);
    }

    private static void Rebuild()
    {
        if (_application is null) return;

        var appMenu = new NativeMenu
        {
            CreateItem("Main.Menu.About", () => Navigate(PageType.Setup, PageSubType.SetupAbout)),
            CreateItem(
                "Main.Menu.Preferences",
                () => Navigate(PageType.Setup, PageSubType.SetupUi),
                new KeyGesture(Key.OemComma, KeyModifiers.Meta)),
            CreateItem(
                "Main.Menu.Hide",
                () => MainWindow.Current?.Hide(),
                new KeyGesture(Key.H, KeyModifiers.Meta)),
            new NativeMenuItemSeparator(),
            CreateItem(
                "Main.Menu.Quit",
                () => MainWindow.Current?.Close(),
                new KeyGesture(Key.Q, KeyModifiers.Meta))
        };
        NativeMenu.SetMenu(_application, appMenu);

        if (_window is null) return;

        var fileMenu = new NativeMenu
        {
            CreateItem(
                "Main.Menu.CloseWindow",
                () => _window.Close(),
                new KeyGesture(Key.W, KeyModifiers.Meta))
        };
        var editMenu = new NativeMenu
        {
            CreateItem(
                "Main.Menu.Undo",
                () => ApplyTextEdit(static text => text.Undo()),
                new KeyGesture(Key.Z, KeyModifiers.Meta)),
            CreateItem(
                "Main.Menu.Redo",
                () => ApplyTextEdit(static text => text.Redo()),
                new KeyGesture(Key.Z, KeyModifiers.Meta | KeyModifiers.Shift)),
            new NativeMenuItemSeparator(),
            CreateItem(
                "Main.Menu.Cut",
                () => ApplyTextEdit(static text => text.Cut()),
                new KeyGesture(Key.X, KeyModifiers.Meta)),
            CreateItem(
                "Main.Menu.Copy",
                () => ApplyTextEdit(static text => text.Copy()),
                new KeyGesture(Key.C, KeyModifiers.Meta)),
            CreateItem(
                "Main.Menu.Paste",
                () => ApplyTextEdit(static text => text.Paste()),
                new KeyGesture(Key.V, KeyModifiers.Meta)),
            CreateItem(
                "Main.Menu.SelectAll",
                () => ApplyTextEdit(static text => text.SelectAll()),
                new KeyGesture(Key.A, KeyModifiers.Meta))
        };
        var windowMenu = new NativeMenu
        {
            CreateItem(
                "Main.Menu.Minimize",
                () =>
                {
                    if (_window is not null)
                        _window.WindowState = WindowState.Minimized;
                },
                new KeyGesture(Key.M, KeyModifiers.Meta))
        };
        var helpMenu = new NativeMenu
        {
            CreateItem("Main.Menu.Help", OpenHelp)
        };

        var menu = new NativeMenu
        {
            new NativeMenuItem(Lang.Text("Main.Menu.File"))
            {
                Menu = fileMenu
            },
            new NativeMenuItem(Lang.Text("Main.Menu.Edit"))
            {
                Menu = editMenu
            },
            new NativeMenuItem(Lang.Text("Main.Menu.Window"))
            {
                Menu = windowMenu
            },
            new NativeMenuItem(Lang.Text("Main.Menu.Help"))
            {
                Menu = helpMenu
            }
        };
        NativeMenu.SetMenu(_window, menu);
    }

    private static NativeMenuItem CreateItem(
        string key,
        Action action,
        KeyGesture? gesture = null)
    {
        var item = new NativeMenuItem(Lang.Text(key));
        if (gesture is not null)
            item.Gesture = gesture;
        item.Click += (_, _) => action();
        return item;
    }

    private static void Navigate(PageType page, PageSubType subType)
    {
        PageRouter.Navigate(page, subType);
    }

    private static void ApplyTextEdit(Action<TextBox> action)
    {
        if (_window?.FocusManager?.GetFocusedElement() is TextBox textBox)
            action(textBox);
    }

    private static void OpenHelp()
    {
        try
        {
            Process.Start(
                new ProcessStartInfo("https://www.bilibili.com/video/BV1uT4y1P7CX")
                {
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[UI] 打开帮助失败：{ex.Message}");
        }
    }
}