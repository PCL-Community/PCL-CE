using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PCL.Core.UI.Dialogs;
using PCL.Core.Utils.OS;
using PCL.UI;
using PCL.UI.Controls;

namespace PCL.Views.Shell;

public sealed class StartupEffects(
    Window window,
    TitleBar titleBar,
    BackgroundLayer background,
    ExtraButtonsPanel extraButtons)
{
    private bool _hasRun;

    private static double WindowTargetOpacity =>
        Math.Clamp(Config.Preference.Theme.WindowOpacity / 1000d + 0.4d, 0.4d, 1d);

    public void Run()
    {
        if (_hasRun) return;
        _hasRun = true;

        try
        {
            UiLogBridge.Install();
            GameFolders.ApplySelectedFolder(States.Game.SelectedFolder);
            LauncherCommon.ModeDebug = Config.Debug.Enabled;
            PrepareWindowAnimation();
            ApplyLockWindowSize();
            titleBar.RefreshTitleAppearance();
            background.Refresh();
            extraButtons.RefreshAllStates();
            RegisterConfigObservers();
            WarmupLoaders();
            MediaService.Current?.RefreshMusic(false, true);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "应用启动界面设置失败");
        }

        Basics.RunInNewThread(ShowStartupDialogs, "StartupDialogs");
    }

    private void ApplyWindowOpacity()
    {
        window.Opacity = WindowTargetOpacity;
    }

    private void PrepareWindowAnimation()
    {
        window.IsHitTestVisible = true;

        var target = WindowTargetOpacity;
        if (window.Content is not Control root)
        {
            window.Opacity = target;
            return;
        }

        root.Opacity = 1d;
        if (!AnimationService.IsEnabled)
        {
            window.Opacity = target;
            return;
        }

        root.RenderTransformOrigin = RelativePoint.TopLeft;
        var rotate = new RotateTransform(-4d);
        var translate = new TranslateTransform(0d, 60d);
        var group = new TransformGroup();
        group.Children.Add(rotate);
        group.Children.Add(translate);
        root.RenderTransform = group;
        window.Opacity = 0d;

        AnimationService.Run(
            new List<IAnimation>
            {
                new OpacityAnimation(window, target, 250),
                new DoubleAnimation(
                    value => translate.Y += value,
                    -translate.Y,
                    600,
                    ease: new BackEaseWithPowerOut(EasePower.Weak)),
                new DoubleAnimation(
                    value => rotate.Angle += value,
                    -rotate.Angle,
                    500,
                    ease: new BackEaseWithPowerOut(EasePower.Weak)),
                new ActionAnimation(
                    () =>
                    {
                        root.RenderTransform = null;
                        LogWrapper.Debug("UI", "窗口进场动画完成");
                    },
                    true)
            },
            "Startup Window Show");
    }

    private void ApplyLockWindowSize()
    {
        window.CanResize = !Config.Preference.LockWindowSize;
    }

    private void RegisterConfigObservers()
    {
        States.Game.SelectedFolderConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => GameFolders.ApplySelectedFolder(e.Value as string)));
        Config.Preference.Theme.WindowOpacityConfig.Observe(
            new ConfigObserver(ConfigEvent.Update, _ => ApplyWindowOpacity()));
        Config.Debug.EnabledConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Update,
                e => LauncherCommon.ModeDebug = e.NewValue is true));
        Config.Preference.LockWindowSizeConfig.Observe(
            new ConfigObserver(ConfigEvent.Update, _ => ApplyLockWindowSize()));
        ConfigService
            .GetConfigItem<string>("UiFont")
            .Observe(
                new ConfigObserver(ConfigEvent.Update, _ => LaunchFonts.InstallGlobalFont()));
        Config.Preference.WindowTitleTypeConfig.Observe(
            new ConfigObserver(ConfigEvent.Update, _ => titleBar.RefreshTitleAppearance()));
        Config.Preference.WindowTitleCustomTextConfig.Observe(
            new ConfigObserver(ConfigEvent.Update, _ => titleBar.RefreshTitleAppearance()));
        Config.Preference.TopBarLeftAlignConfig.Observe(
            new ConfigObserver(ConfigEvent.Update, _ => titleBar.RefreshTitleAppearance()));

        Config.Preference.Background.BackgroundColorfulConfig.Observe(
            new ConfigObserver(ConfigEvent.Update, _ => background.RefreshColor()));
        Config.Preference.Background.WallpaperOpacityConfig.Observe(
            new ConfigObserver(ConfigEvent.Update, _ => background.ApplyPresentation()));
        Config.Preference.Background.WallpaperSuitModeConfig.Observe(
            new ConfigObserver(ConfigEvent.Update, _ => background.ApplyPresentation()));
        Config.Preference.Background.WallpaperBlurRadiusConfig.Observe(
            new ConfigObserver(ConfigEvent.Update, _ => background.ApplyPresentation()));
    }

    private static void WarmupLoaders()
    {
        try
        {
            GameDownload.dlClientListMojangLoader.Start(1);
            UpdateManager.serverLoader.Start(1);
            States.System.StartupCount += 1;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "初始化加载池失败");
        }

        try
        {
            var lastVersion = States.System.LastVersion;
            if (lastVersion != Basics.VersionCode)
            {
                LogWrapper.Info("UI", $"版本号从 {lastVersion} 变更为 {Basics.VersionCode}");
                States.System.LastVersion = Basics.VersionCode;
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "记录版本号失败");
        }
    }

    private static async void ShowStartupDialogs()
    {
        try
        {
            ShowSpecialVersionHint();
            await ShowEnvironmentWarningAsync();
            await ShowEulaDialogAsync();
            await ShowTelemetryDialogAsync();
            HardwareInfo.GetHardwareInfo();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "启动弹窗提示失败");
        }
    }

    private static void ShowSpecialVersionHint()
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("PCL_DISABLE_DEBUG_HINT") is not null) return;
        DialogService.Instance.ShowNotification(
            new TextDialogRequest
            {
                Title = Lang.Text("Main.SpecialVersion.Title"),
                Content = Lang.Text("Main.SpecialVersion.DebugHint")
                    + Environment.NewLine
                    + Environment.NewLine
                    + Lang.Text("Main.SpecialVersion.HideHintNotice"),
                Buttons = DialogButtons.From(
                    DialogButton.Create(Lang.Text("Main.SpecialVersion.IUnderstand")),
                    DialogButton.Create(
                        Lang.Text("Main.SpecialVersion.OpenDownloadPageAndExit"),
                        () =>
                        {
                            OpenWebsite("https://github.com/PCL-Community/PCL2-CE/releases/latest");
                            UiInteraction.Shutdown();
                        })),
                IsWarn = true
            });
#endif
    }

    private static async Task ShowEnvironmentWarningAsync()
    {
        try
        {
            var service = ApplicationRuntime.GetService<IRuntimeEnvironmentService>()
                ?? throw new InvalidOperationException("运行环境检查服务尚未初始化");
            var issues = service.GetIssues(Basics.ExecutablePath);
            if (issues.Count == 0) return;
            LogWrapper.Info("UI", $"启动环境检查发现问题：{string.Join(", ", issues)}");

            var problems = issues.Select(issue => Lang.Text(
                issue switch
                {
                    RuntimeEnvironmentIssue.OutdatedSystem =>
                        "Application.EnvironmentWarning.WindowsVersion",
                    RuntimeEnvironmentIssue.System32Bit =>
                        "Application.EnvironmentWarning.System32Bit",
                    RuntimeEnvironmentIssue.TempFolder =>
                        "Application.EnvironmentWarning.TempFolder",
                    RuntimeEnvironmentIssue.SocialSoftwareFolder =>
                        "Application.EnvironmentWarning.SocialSoftwareFolder",
                    _ => throw new ArgumentOutOfRangeException(nameof(issue), issue, null)
                }));

            await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Application.EnvironmentWarning.Title"),
                    Content = Lang.Text(
                        "Application.EnvironmentWarning.Message",
                        string.Join(Environment.NewLine, problems)),
                    Buttons = DialogButtons.Create("Application.EnvironmentWarning.IKnow"),
                    IsWarn = true
                });
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "UI", "启动环境检查失败");
        }
    }

    private static async Task ShowEulaDialogAsync()
    {
        if (States.System.LauncherEula) return;

        var button = (await DialogService.Instance.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Main.Eula.Title"),
                Content = Lang.Text("Main.Eula.Message"),
                Buttons = DialogButtons.From(
                    DialogButton.Create(Lang.Text("Common.Action.Agree")),
                    DialogButton.Create(Lang.Text("Common.Action.Decline")),
                    DialogButton.Create(
                        Lang.Text("Main.Eula.View"),
                        () => OpenWebsite("https://shimo.im/docs/rGrd8pY8xWkt6ryW")))
            })).Button;

        switch (button)
        {
            case 1:
                States.System.LauncherEula = true;
                break;
            case 2:
                UiInteraction.Shutdown();
                break;
        }
    }

    private static async Task ShowTelemetryDialogAsync()
    {
        if (!Config.System.TelemetryConfig.IsDefault()) return;

        var button = (await DialogService.Instance.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Main.Telemetry.Title"),
                Content = Lang.Text("Main.Telemetry.Message"),
                Buttons = DialogButtons.AgreeDecline
            })).Button;
        Config.System.TelemetryConfig.SetValue(button == 1, forceNewValue: true);
    }

    private static void OpenWebsite(string url)
    {
        try
        {
            Process.Start(
                new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("UI", $"打开网页失败：{ex.Message}");
        }
    }
}