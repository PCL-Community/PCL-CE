using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Navigation;
using PCL.Views;
using PlainToolkit.ObjectiveC.Runtime;

namespace PCL;

internal interface IPlatformShellIntegration
{
    void OnFrameworkInitialized(Application application);
    void OnWindowCreated(Window window);
}

internal sealed class WindowsShellIntegration : IPlatformShellIntegration
{
    public void OnFrameworkInitialized(Application application)
    {
    }

    public void OnWindowCreated(Window window)
    {
    }
}

internal sealed class LinuxShellIntegration : IPlatformShellIntegration
{
    public void OnFrameworkInitialized(Application application)
    {
    }

    public void OnWindowCreated(Window window)
    {
    }
}

internal sealed class MacOsShellIntegration : IPlatformShellIntegration
{
    private static readonly ObjCSel Alloc = ObjCSel.FromString("alloc");

    private static readonly ObjCSel InitWithBytesLength =
        ObjCSel.FromString("initWithBytes:length:");

    private static readonly ObjCSel InitWithData = ObjCSel.FromString("initWithData:");
    private static readonly ObjCSel Release = ObjCSel.FromString("release");

    private static readonly ObjCSel SharedApplication =
        ObjCSel.FromString("sharedApplication");

    private static readonly ObjCSel SetApplicationIconImage =
        ObjCSel.FromString("setApplicationIconImage:");

    private static readonly ObjCSel _Hide = ObjCSel.FromString("hide:");
    private static readonly ObjCSel _HideOtherApplications = ObjCSel.FromString("hideOtherApplications:");
    private static readonly ObjCSel _UnhideAllApplications = ObjCSel.FromString("unhideAllApplications:");

    private static readonly ObjCSel _InitWithIdentifier = ObjCSel.FromString("initWithIdentifier:");
    private static readonly ObjCSel _SetToolbar = ObjCSel.FromString("setToolbar:");
    private static readonly ObjCSel _SetToolbarStyle = ObjCSel.FromString("setToolbarStyle:");
    private static readonly ObjCSel _SetTitlebarSeparatorStyle = ObjCSel.FromString("setTitlebarSeparatorStyle:");

    private const nint AutomaticToolbarStyle = 0;
    private const nint UnifiedToolbarStyle = 3;
    private const nint AutomaticTitlebarSeparator = 0;
    private const nint NoTitlebarSeparator = 1;
    private const double UnifiedTitleBarLeadingInset = 74d;

    private static bool _frameworksLoaded;

    public void OnFrameworkInitialized(Application application)
    {
        ApplyDockIcon();
    }

    public void OnWindowCreated(Window window)
    {
        if (window is MainWindow mainWindow)
        {
            mainWindow.UseSystemWindowChrome();
            _ApplyTitleBarStyle(mainWindow);
            Config.Preference.MacTitleBarStyleConfig.Observe(
                new ConfigObserver(
                    ConfigEvent.Update,
                    _ => Dispatcher.UIThread.Post(() => _ApplyTitleBarStyle(mainWindow))));
        }

        _AttachMenus(window);
    }

    private static void _ApplyTitleBarStyle(MainWindow window)
    {
        var unified = Config.Preference.MacTitleBarStyle >= 1;
        var height = window.Height;
        window.ExtendClientAreaToDecorationsHint = unified;
        if (unified)
        {
            _AttachUnifiedToolbar(window);
            // 先设为 0 再设回 -1，是为了让 Avalonia 重新读取系统标题栏的高度
            window.ExtendClientAreaTitleBarHeightHint = 0d;
            window.ExtendClientAreaTitleBarHeightHint = -1d;
        }
        else
        {
            _ResetTitleBarStyles(window);
        }

        window.Height = height;
        window.ApplyExtendedTitleBarLayout(unified ? UnifiedTitleBarLeadingInset : 0d);
    }

    private static void _AttachUnifiedToolbar(Window window)
    {
        if (window.TryGetPlatformHandle()?.Handle is not { } handle || handle == 0) return;
        EnsureFrameworks();

        var nsWindow = new ObjCObject(handle);
        var identifier = ObjCString.FromString("PCL.TitleBar");
        try
        {
            var toolbar = ObjCObject
                .GetClass("NSToolbar")
                .SendMessage<ObjCObject>(Alloc)
                .SendMessage<nint, ObjCObject>(_InitWithIdentifier, identifier.Value);
            nsWindow.SendMessage(_SetToolbar, toolbar);
            toolbar.SendMessage(Release);
        }
        finally
        {
            if (!identifier.IsNull) new ObjCObject(identifier.Value).SendMessage(Release);
        }

        nsWindow.SendMessage(_SetToolbarStyle, UnifiedToolbarStyle);
        nsWindow.SendMessage(_SetTitlebarSeparatorStyle, NoTitlebarSeparator);
    }

    private static void _ResetTitleBarStyles(Window window)
    {
        if (window.TryGetPlatformHandle()?.Handle is not { } handle || handle == 0) return;
        EnsureFrameworks();

        var nsWindow = new ObjCObject(handle);
        nsWindow.SendMessage(_SetToolbarStyle, AutomaticToolbarStyle);
        nsWindow.SendMessage(_SetTitlebarSeparatorStyle, AutomaticTitlebarSeparator);
    }

    private static void _AttachMenus(Window window)
    {
        if (Application.Current is not { } application
            || NativeMenu.GetMenu(application) is not { Items.Count: 0 } appMenu)
            return;

        appMenu.Add(
            _CreateMenuItem(
                "Main.Menu.About",
                () => _ShowPage(window, () => PageRouter.Navigate(PageIds.Setup, SubPageIds.SetupAbout))));
        appMenu.Add(new NativeMenuItemSeparator());
        appMenu.Add(
            _CreateMenuItem(
                "Main.Menu.Settings",
                () => _ShowPage(window, () => PageRouter.Navigate(PageIds.Setup)),
                new KeyGesture(Key.OemComma, KeyModifiers.Meta)));
        appMenu.Add(new NativeMenuItemSeparator());
        appMenu.Add(
            _CreateMenuItem(
                "Main.Menu.Hide",
                () => _SendToApplication(_Hide),
                new KeyGesture(Key.H, KeyModifiers.Meta)));
        appMenu.Add(
            _CreateMenuItem(
                "Main.Menu.HideOthers",
                () => _SendToApplication(_HideOtherApplications),
                new KeyGesture(Key.H, KeyModifiers.Meta | KeyModifiers.Alt)));
        appMenu.Add(_CreateMenuItem("Main.Menu.ShowAll", () => _SendToApplication(_UnhideAllApplications)));
        appMenu.Add(new NativeMenuItemSeparator());
        appMenu.Add(
            _CreateMenuItem(
                "Main.Menu.Quit",
                () => (application.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.TryShutdown(),
                new KeyGesture(Key.Q, KeyModifiers.Meta)));

        var windowMenu = new NativeMenu();
        windowMenu.Add(
            _CreateMenuItem(
                "Main.Menu.Minimize",
                () => window.WindowState = WindowState.Minimized,
                new KeyGesture(Key.M, KeyModifiers.Meta)));
        windowMenu.Add(
            _CreateMenuItem("Main.Menu.CloseWindow", window.Close, new KeyGesture(Key.W, KeyModifiers.Meta)));

        var windowMenuItem = new NativeMenuItem { Menu = windowMenu };
        windowMenuItem.SetLocalizedValue(NativeMenuItem.HeaderProperty, "Main.Menu.Window");
        var menuBar = new NativeMenu();
        menuBar.Add(windowMenuItem);
        NativeMenu.SetMenu(window, menuBar);
    }

    private static NativeMenuItem _CreateMenuItem(string key, Action action, KeyGesture? gesture = null)
    {
        var item = new NativeMenuItem { Gesture = gesture };
        item.SetLocalizedValue(NativeMenuItem.HeaderProperty, key);
        item.Click += (_, _) => action();
        return item;
    }

    private static void _ShowPage(Window window, Action navigate)
    {
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
        navigate();
    }

    private static void _SendToApplication(ObjCSel selector)
    {
        EnsureFrameworks();
        ObjCObject
            .GetClass("NSApplication")
            .SendMessage<ObjCObject>(SharedApplication)
            .SendMessage(selector, default(ObjCObject));
    }

    private static void ApplyDockIcon()
    {
        EnsureFrameworks();

        using var stream = AssetLoader.Open(
            new Uri("avares://PCL.Launcher/Assets/Images/icon-mac.png"));
        var icon = new byte[stream.Length];
        stream.ReadExactly(icon);

        var pinned = GCHandle.Alloc(icon, GCHandleType.Pinned);
        var data = default(ObjCObject);
        var image = default(ObjCObject);
        try
        {
            data = ObjCObject
                .GetClass("NSData")
                .SendMessage<ObjCObject>(Alloc)
                .SendMessage<nint, nuint, ObjCObject>(
                    InitWithBytesLength,
                    pinned.AddrOfPinnedObject(),
                    (nuint)icon.Length);

            image = ObjCObject
                .GetClass("NSImage")
                .SendMessage<ObjCObject>(Alloc)
                .SendMessage<ObjCObject, ObjCObject>(InitWithData, data);

            data.SendMessage(Release);
            data = default;

            ObjCObject
                .GetClass("NSApplication")
                .SendMessage<ObjCObject>(SharedApplication)
                .SendMessage(SetApplicationIconImage, image);
        }
        finally
        {
            if (!data.IsNull) data.SendMessage(Release);
            if (!image.IsNull) image.SendMessage(Release);
            pinned.Free();
        }
    }

    private static void EnsureFrameworks()
    {
        if (_frameworksLoaded) return;

        NativeLibrary.Load(
            "/System/Library/Frameworks/Foundation.framework/Foundation");
        NativeLibrary.Load(
            "/System/Library/Frameworks/AppKit.framework/AppKit");
        _frameworksLoaded = true;
    }
}