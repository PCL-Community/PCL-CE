using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>界面个性化设置页的视图模型。</summary>
public partial class PageSetupUIViewModel : ViewModelBase
{
    [ObservableProperty] private bool _autoPauseVideoVisible = true;

    [ObservableProperty] private int _backgroundBlur =
        Config.Preference.Background.WallpaperBlurRadius;

    [ObservableProperty] private bool _backgroundBlurVisible;

    [ObservableProperty] private bool _backgroundClearEnabled = true;

    [ObservableProperty] private bool _backgroundClearVisible;

    [ObservableProperty] private int _backgroundOpacity =
        Config.Preference.Background.WallpaperOpacity;

    [ObservableProperty] private bool _backgroundOpacityVisible;

    [ObservableProperty] private int _backgroundSuitSelectedIndex;

    [ObservableProperty] private bool _backgroundSuitVisible;

    [ObservableProperty]
    private string _backgroundTitle = Lang.Text("Setup.Ui.Background.TitleDefault");

    [ObservableProperty] private int _blurRadius = Config.Preference.Blur.Radius;

    [ObservableProperty] private int _blurSamplingRate =
        Config.Preference.Blur.SamplingRate;

    [ObservableProperty] private int _blurTypeSelectedIndex = -1;

    [ObservableProperty] private string _customNetText = string.Empty;

    [ObservableProperty] private int _customPresetSelectedIndex = -1;

    [ObservableProperty] private int _darkColorSelectedIndex = -1;

    [ObservableProperty] private int _darkModeSelectedIndex = -1;

    [ObservableProperty] private string _featureHideTitle = Lang.Text("Setup.Ui.FeatureHide.Title");

    [ObservableProperty] private bool _featureHideVisible = true;
    [ObservableProperty] private bool _launcherHideVisible;

    [ObservableProperty] private int _launcherOpacity = Config.Preference.Theme.WindowOpacity;

    [ObservableProperty] private int _lightColorSelectedIndex = -1;

    [ObservableProperty] private string _logoText = string.Empty;

    [ObservableProperty] private bool _musicClearEnabled = true;

    [ObservableProperty] private bool _musicClearVisible;

    [ObservableProperty] private bool _musicDetailVisible;

    [ObservableProperty] private string _musicTitle = Lang.Text("Setup.Ui.Music.Title");

    [ObservableProperty] private int _musicVolume = Config.Preference.Music.Volume;

    [ObservableProperty] private bool _musicVolumeVisible;

    private bool _reloading;

    partial void OnDarkModeSelectedIndexChanged(int value)
    {
        if (!_reloading && value >= 0)
            Config.Preference.Theme.ColorMode = (ColorMode)value;
    }

    partial void OnLightColorSelectedIndexChanged(int value)
    {
        if (!_reloading && value >= 0)
            Config.Preference.Theme.LightColor = (ColorTheme)value;
    }

    partial void OnDarkColorSelectedIndexChanged(int value)
    {
        if (!_reloading && value >= 0)
            Config.Preference.Theme.DarkColor = (ColorTheme)value;
    }

    partial void OnBlurTypeSelectedIndexChanged(int value)
    {
        if (!_reloading && value >= 0)
            Config.Preference.Blur.KernelType = value;
    }

    partial void OnBackgroundSuitSelectedIndexChanged(int value)
    {
        if (!_reloading && value >= 0)
            Config.Preference.Background.WallpaperSuitMode = value;
    }

    partial void OnCustomPresetSelectedIndexChanged(int value)
    {
        if (!_reloading && value >= 0)
            Config.Preference.Homepage.SelectedPreset = value;
    }

    partial void OnLogoTextChanged(string value)
    {
        if (!_reloading)
            Config.Preference.WindowTitleCustomText = value ?? string.Empty;
    }

    partial void OnLauncherOpacityChanged(int value)
    {
        if (!_reloading) Config.Preference.Theme.WindowOpacity = value;
    }

    partial void OnBackgroundOpacityChanged(int value)
    {
        if (!_reloading) Config.Preference.Background.WallpaperOpacity = value;
    }

    partial void OnBackgroundBlurChanged(int value)
    {
        if (!_reloading) Config.Preference.Background.WallpaperBlurRadius = value;
    }

    partial void OnBlurRadiusChanged(int value)
    {
        if (!_reloading) Config.Preference.Blur.Radius = value;
    }

    partial void OnBlurSamplingRateChanged(int value)
    {
        if (!_reloading) Config.Preference.Blur.SamplingRate = value;
    }

    partial void OnMusicVolumeChanged(int value)
    {
        if (!_reloading) Config.Preference.Music.Volume = value;
    }

    public void Reload(int themeColorCount = 4)
    {
        _reloading = true;
        try
        {
            var theme = Config.Preference.Theme;
            var colorMax = Math.Max(0, themeColorCount - 1);
            LauncherOpacity = theme.WindowOpacity;
            DarkModeSelectedIndex = Math.Clamp((int)theme.ColorMode, 0, 2);
            LightColorSelectedIndex = Math.Clamp((int)theme.LightColor, 0, colorMax);
            DarkColorSelectedIndex = Math.Clamp((int)theme.DarkColor, 0, colorMax);

            BlurRadius = Config.Preference.Blur.Radius;
            BlurSamplingRate = Config.Preference.Blur.SamplingRate;
            BlurTypeSelectedIndex = Math.Clamp(
                Config.Preference.Blur.KernelType,
                0,
                1);

            BackgroundOpacity = Config.Preference.Background.WallpaperOpacity;
            BackgroundBlur = Config.Preference.Background.WallpaperBlurRadius;
            BackgroundSuitSelectedIndex = Math.Clamp(
                Config.Preference.Background.WallpaperSuitMode,
                0,
                8);

            LogoText = Config.Preference.WindowTitleCustomText;
            CustomNetText = Config.Preference.Homepage.CustomUrl;
            CustomPresetSelectedIndex = Math.Clamp(
                Config.Preference.Homepage.SelectedPreset,
                0,
                15);

            MusicVolume = Config.Preference.Music.Volume;
        }
        finally
        {
            _reloading = false;
        }
    }

    public void ShowLauncherHint()
    {
        LauncherHideVisible = true;
    }

    public void ApplyBackgroundState(bool show, int count)
    {
        BackgroundOpacityVisible = show;
        BackgroundBlurVisible = show;
        BackgroundSuitVisible = show;
        BackgroundClearVisible = show;
        AutoPauseVideoVisible = show;
        BackgroundTitle = show
            ? Lang.Text("Setup.Ui.Background.TitleWithCount", count)
            : Lang.Text("Setup.Ui.Background.TitleDefault");
    }

    public void ApplyMusicState(bool hasMusic, int count)
    {
        MusicVolumeVisible = hasMusic;
        MusicDetailVisible = hasMusic;
        MusicClearVisible = hasMusic;
        MusicTitle = hasMusic
            ? Lang.Text("Setup.Ui.Music.TitleWithCount", count)
            : Lang.Text("Setup.Ui.Music.Title");
    }

    public void ApplyFeatureHideState(bool forceShow, bool functionHidden)
    {
        FeatureHideVisible = forceShow || !functionHidden;
        FeatureHideTitle = forceShow
            ? Lang.Text("Setup.Ui.FeatureHide.TitleTemporarilyDisabled")
            : Lang.Text("Setup.Ui.FeatureHide.Title");
    }

    public async Task<bool> ConfirmBackgroundClearAsync()
    {
        return (await Dialog.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Common.Dialog.Warning"),
                Content = Lang.Text("Setup.Ui.Background.Clear.Confirm.Message"),
                Buttons = DialogButtons.OkCancel,
                IsWarn = true
            })).IsConfirmed;
    }

    public async Task<bool> ConfirmMusicClearAsync()
    {
        return (await Dialog.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Common.Dialog.Warning"),
                Content = Lang.Text("Setup.Ui.Music.Clear.Confirm.Message"),
                Buttons = DialogButtons.OkCancel,
                IsWarn = true
            })).IsConfirmed;
    }

    /// <summary>统计目录中的内容文件数量；与 WPF 一致排除 ini/db 等配置与缓存文件。</summary>
    public static int CountContentFiles(string directory)
    {
        if (!Directory.Exists(directory)) return 0;
        try
        {
            return Directory
                .EnumerateFiles(directory)
                .Count(file =>
                {
                    var extension = Path.GetExtension(file);
                    return !extension.Equals(".ini", StringComparison.OrdinalIgnoreCase)
                        && !extension.Equals(".db", StringComparison.OrdinalIgnoreCase);
                });
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public static object FormatMusicVolumeHint(object value)
    {
        return Lang.Number(Math.Ceiling(Convert.ToDouble(value) * 0.1d) / 100d, "P0");
    }

    public static object FormatLauncherOpacityHint(object value)
    {
        return Lang.Number(Math.Round(40 + Convert.ToDouble(value) * 0.1d) / 100d, "P0");
    }

    public static object FormatBackgroundOpacityHint(object value)
    {
        return Lang.Number(Math.Round(Convert.ToDouble(value) * 0.1d) / 100d, "P0");
    }

    public static object FormatPixelHint(object value)
    {
        return Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(value), "N0"));
    }

    public static object FormatSamplingRateHint(object value)
    {
        return Lang.Number(Convert.ToDouble(value) / 100d, "P0");
    }
}