using PCL.Core.App;
using PCL.Core.App.Configuration;

namespace PCL;

public partial class Setup
{
    public Setup()
    {
        ConfigService.RegisterObserver(
            Config.Preference.Hide,
            new ConfigObserver(ConfigEvent.Changed, _ => PageSetupUI.HiddenRefresh()));

        Config.Launch.MemoryAllocationModeConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => LaunchRamType((int)e.Value!)));
        States.Game.SelectedFolderConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => LaunchFolderSelect((string)(e.Value ?? ""))));
        States.Game.SelectedInstanceConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => LaunchInstanceSelect((string)(e.Value ?? ""))));

        Config.Download.ThreadLimitConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => ToolDownloadThread((int)e.Value!)));
        Config.Download.FileConnectionLimitConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => ToolDownloadFileConnection((int)e.Value!)));
        Config.Download.SpeedLimitConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => ToolDownloadSpeed((int)e.Value!)));

        Config.Preference.Theme.WindowOpacityConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiLauncherTransparent((int)e.Value!)));
        Config.Preference.Theme.ThemeSelectedConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiLauncherTheme((int)e.Value!)));
        Config.Preference.Background.BackgroundColorfulConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiBackgroundColorful((bool)e.Value!)));
        Config.Preference.LockWindowSizeConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiLockWindowSize((bool)e.Value!)));

        Config.Preference.Background.AutoPauseVideoConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiAutoPauseVideo((bool)e.Value!)));

        Config.Preference.Background.WallpaperOpacityConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiBackgroundOpacity((int)e.Value!)));
        Config.Preference.Background.WallpaperBlurRadiusConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiBackgroundBlur((int)e.Value!)));
        Config.Preference.Background.WallpaperSuitModeConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiBackgroundSuit((int)e.Value!)));

        Config.Preference.FontConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiFont((string)(e.Value ?? ""))));

        Config.Preference.Homepage.TypeConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiCustomType((int)e.Value!)));

        Config.Preference.Blur.IsEnabledConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiBlur((bool)e.Value!)));
        Config.Preference.Blur.RadiusConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiBlurValue((int)e.Value!)));
        Config.Preference.Blur.SamplingRateConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiBlurSamplingRate((int)e.Value!)));
        Config.Preference.Blur.KernelTypeConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiBlurType((int)e.Value!)));

        Config.Preference.WindowTitleTypeConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiLogoType((int)e.Value!)));
        Config.Preference.WindowTitleCustomTextConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiLogoText((string)(e.Value ?? ""))));
        Config.Preference.TopBarLeftAlignConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => UiLogoLeft((bool)e.Value!)));

        Config.Debug.EnabledConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => SystemDebugMode((bool)e.Value!)));
        Config.Debug.AnimationSpeedConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => SystemDebugAnim((int)e.Value!)));
        Config.Network.HttpProxy.CustomAddressConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => SystemHttpProxy((string)(e.Value ?? ""))));
        Config.Network.HttpProxy.TypeConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => SystemHttpProxyType((int)e.Value!)));
        Config.Network.HttpProxy.CustomUsernameConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => SystemHttpProxyCustomUsername((string)(e.Value ?? ""))));
        Config.Network.HttpProxy.CustomPasswordConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => SystemHttpProxyCustomPassword((string)(e.Value ?? ""))));

        Config.Instance.MemorySolutionConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => VersionRamType((int)e.Value!)));
        Config.InstanceAuth.LoginRequirementSolutionConfig.Observe(
            new ConfigObserver(
                ConfigEvent.Changed,
                e => VersionServerLogin((int)e.Value!)));
    }

    /// <summary>
    ///     主动应用所有当前配置值。
    /// </summary>
    public static void ApplyAll()
    {
        LaunchRamType(Config.Launch.MemoryAllocationMode);

        ToolDownloadThread(Config.Download.ThreadLimit);
        ToolDownloadFileConnection(Config.Download.FileConnectionLimit);
        ToolDownloadSpeed(Config.Download.SpeedLimit);

        UiLauncherTransparent(Config.Preference.Theme.WindowOpacity);
        UiLauncherTheme(Config.Preference.Theme.ThemeSelected);
        UiBackgroundColorful(Config.Preference.Background.BackgroundColorful);
        UiLockWindowSize(Config.Preference.LockWindowSize);

        UiAutoPauseVideo(Config.Preference.Background.AutoPauseVideo);

        UiBackgroundOpacity(Config.Preference.Background.WallpaperOpacity);
        UiBackgroundBlur(Config.Preference.Background.WallpaperBlurRadius);
        UiBackgroundSuit(Config.Preference.Background.WallpaperSuitMode);

        UiFont(Config.Preference.Font);

        UiCustomType(Config.Preference.Homepage.Type);

        if (Config.Preference.Blur.IsEnabled)
        {
            UiBlurValue(Config.Preference.Blur.Radius);
            UiBlurSamplingRate(Config.Preference.Blur.SamplingRate);
            UiBlurType(Config.Preference.Blur.KernelType);
        }
        else
        {
            UiBlurValue(0);
        }

        UiBlur(Config.Preference.Blur.IsEnabled);

        UiLogoType((int)Config.Preference.WindowTitleType);
        UiLogoText(Config.Preference.WindowTitleCustomText);
        UiLogoLeft(Config.Preference.TopBarLeftAlign);

        PageSetupUI.HiddenRefresh();

        SystemDebugMode(Config.Debug.Enabled);
        SystemDebugAnim(Config.Debug.AnimationSpeed);
        SystemHttpProxy(Config.Network.HttpProxy.CustomAddress);
        SystemHttpProxyType(Config.Network.HttpProxy.Type);
        SystemHttpProxyCustomUsername(Config.Network.HttpProxy.CustomUsername);
        SystemHttpProxyCustomPassword(Config.Network.HttpProxy.CustomPassword);
    }
}
