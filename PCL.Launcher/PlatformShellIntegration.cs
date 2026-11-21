using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform;
using PCL.Navigation;
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

    private static bool _frameworksLoaded;

    public void OnFrameworkInitialized(Application application)
    {
        ApplyDockIcon();
    }

    public void OnWindowCreated(Window window)
    {
        _AttachMenus(window);
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