using Avalonia;
using Avalonia.Markup.Xaml;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.UI.Controls;
using PCL.UI.Controls.Behaviors;
using PCL.UI.Theming;
using PCL.Views.Shell;

namespace PCL;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        LaunchFonts.InstallGlobalFont();
        LocalizationService.LanguageChanged += LaunchFonts.InstallGlobalFont;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ThemeCoordinator.Install();
        Tooltip.Enable();
        ClipboardInterceptor.Enable();

        SplashWindow? splash = null;
        try
        {
            if (ConfigService.IsInitialized && Config.Preference.ShowStartupLogo)
            {
                splash = new SplashWindow();
                splash.Show();
            }
        }
        catch
        {
            splash?.Close();
            splash = null;
        }

        ApplicationHost.NotifyStartup(this);
        if (splash is not null)
            Lifecycle.When(LifecycleState.Running, () => splash.Close());

        base.OnFrameworkInitializationCompleted();
    }
}