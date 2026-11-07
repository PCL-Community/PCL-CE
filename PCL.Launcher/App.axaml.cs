using Avalonia;
using Avalonia.Markup.Xaml;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.UI.Controls;
using PCL.UI.Controls.Behaviors;
using PCL.UI.SystemIntegration;
using PCL.UI.Theming;
using PCL.Views;
using PCL.Views.Shell;

namespace PCL;

public class App : Application
{
    private static SplashWindow? _splashWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        LaunchFonts.InstallGlobalFont();
        LocalizationService.LanguageChanged += LaunchFonts.InstallGlobalFont;
        LogWrapper.Debug("UI", $"注册启动画面：当前生命周期 {Lifecycle.CurrentState}");
        Lifecycle.When(LifecycleState.Loaded, ShowStartupSplash);
        Lifecycle.When(LifecycleState.WindowCreated, CloseStartupSplash);
        Lifecycle.When(
            LifecycleState.WindowCreated,
            () =>
            {
                if (MainWindow.Current is { } window)
                    MacNativeMenu.AttachWindow(window);
            });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ThemeCoordinator.Install();
        Tooltip.Enable();
        ClipboardInterceptor.Enable();
        MacNativeMenu.Install(this);

        ApplicationHost.NotifyStartup(this);
        base.OnFrameworkInitializationCompleted();
    }

    private static void ShowStartupSplash()
    {
        if (!ConfigService.IsInitialized)
        {
            LogWrapper.Debug("UI", "启动画面跳过：配置尚未初始化");
            return;
        }

        if (!Config.Preference.ShowStartupLogo)
        {
            LogWrapper.Debug("UI", "启动画面跳过：配置已关闭");
            return;
        }

        try
        {
            _splashWindow = new SplashWindow();
            _splashWindow.Show();
            LogWrapper.Debug("UI", "启动画面已显示");
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "UI", "启动画面显示失败");
            _splashWindow = null;
        }
    }

    private static void CloseStartupSplash()
    {
        var splash = _splashWindow;
        _splashWindow = null;
        if (splash is null)
            return;

        try
        {
            splash.Close();
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "UI", "关闭启动画面失败");
        }
    }
}