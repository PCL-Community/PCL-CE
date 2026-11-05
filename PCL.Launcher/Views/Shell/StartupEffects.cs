using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.Core.Utils.OS;
using PCL.UI;
using PCL.UI.Animations;
using static PCL.Animations;

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
            PrepareWindowAnimation();
            ApplyLockWindowSize();
            titleBar.RefreshTitleAppearance();
            background.Refresh();
            extraButtons.RefreshAllStates();
            RegisterConfigObservers();
            WarmupLoaders();
            // TODO(SHL-03): 背景音乐引擎（NAudio）尚未迁移，启动时的音乐状态刷新暂不执行
            LogWrapper.Info("UI", "背景音乐引擎未迁移，启动音乐状态刷新已跳过");
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
        var target = WindowTargetOpacity;
        if (window.Content is not Control root || !AnimationService.IsEnabled)
        {
            window.Opacity = target;
            return;
        }

        if (!root.IsLoaded)
        {
            void OnLoaded(object? sender, RoutedEventArgs e)
            {
                root.Loaded -= OnLoaded;
                PrepareWindowAnimation();
            }

            root.Loaded += OnLoaded;
            window.Opacity = 0d;
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

        AniStart(
            new List<AniData>
            {
                AaOpacity(window, target, 250, 100),
                AaDouble(
                    value => translate.Y += value,
                    -translate.Y,
                    600,
                    100,
                    new AniEaseOutBack(AniEasePower.Weak)),
                AaDouble(
                    value => rotate.Angle += value,
                    -rotate.Angle,
                    500,
                    100,
                    new AniEaseOutBack(AniEasePower.Weak)),
                AaCode(
                    () =>
                    {
                        root.RenderTransform = null;
                        LogWrapper.Debug("UI", "窗口进场动画完成");
                    },
                    after: true)
            },
            "Startup Window Show");
    }

    private void ApplyLockWindowSize()
    {
        window.CanResize = !Config.Preference.LockWindowSize;
    }

    private void RegisterConfigObservers()
    {
        Config.Preference.Theme.WindowOpacityConfig.Observe(
            new ConfigObserver(ConfigEvent.Update, _ => ApplyWindowOpacity()));
        Config.Preference.LockWindowSizeConfig.Observe(
            new ConfigObserver(ConfigEvent.Update, _ => ApplyLockWindowSize()));
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

    private static void ShowStartupDialogs()
    {
        try
        {
            ShowSpecialVersionHint();
            ShowEulaDialog();
            ShowTelemetryDialog();
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
        _ = DialogService.Instance.Show(
            new TextDialogRequest
            {
                Title = Lang.Text("Main.SpecialVersion.Title"),
                Content = Lang.Text("Main.SpecialVersion.DebugHint")
                          + Environment.NewLine + Environment.NewLine
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
                IsWarn = true,
                ForceWait = true
            });
#endif
    }

    private static void ShowEulaDialog()
    {
        if (States.System.LauncherEula) return;

        var button = DialogService.Instance.Show(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Main.Eula.Title"),
                        Content = Lang.Text("Main.Eula.Message"),
                        Buttons = DialogButtons.From(
                            DialogButton.Create(Lang.Text("Common.Action.Agree")),
                            DialogButton.Create(Lang.Text("Common.Action.Decline")),
                            DialogButton.Create(
                                Lang.Text("Main.Eula.View"),
                                () => OpenWebsite("https://shimo.im/docs/rGrd8pY8xWkt6ryW"))),
                        ForceWait = true
                    })
                .Result?.Button
            ?? 0;

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

    private static void ShowTelemetryDialog()
    {
        if (!Config.System.TelemetryConfig.IsDefault()) return;

        var button = DialogService.Instance.Show(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Main.Telemetry.Title"),
                        Content = Lang.Text("Main.Telemetry.Message"),
                        Buttons = DialogButtons.AgreeDecline,
                        ForceWait = true
                    })
                .Result?.Button
            ?? 0;
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
