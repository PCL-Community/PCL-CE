using Avalonia;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.UI.Controls;
using PCL.UI.Controls.Behaviors;
using PCL.UI.Diagnostics;
using PCL.UI.Theming;

namespace PCL;

public class App : Application
{
    public override void Initialize()
    {
#if DEBUG
        Logger.Sink = new BindingLogSink();
#endif
        AvaloniaXamlLoader.Load(this);
        LaunchFonts.InstallGlobalFont();
        LocalizationService.LanguageChanged += LaunchFonts.InstallGlobalFont;
        Lifecycle.When(
            LifecycleState.WindowCreated,
            () =>
            {
                if (Lifecycle.CurrentApplication?.MainWindow is IAvaloniaWindowHost window)
                    PlatformStartup.Shell.OnWindowCreated(window.Window);
            });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ThemeCoordinator.Install();
        Tooltip.Enable();
        ClipboardInterceptor.Enable();
        PlatformStartup.Shell.OnFrameworkInitialized(this);

        if (Lifecycle.CurrentApplication is IAvaloniaApplicationHost host)
            host.Attach(this);
        base.OnFrameworkInitializationCompleted();
    }
}