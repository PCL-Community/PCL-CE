using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>界面个性化设置页的视图模型。</summary>
public partial class PageSetupUIViewModel : ViewModelBase
{
    [ObservableProperty] private int _backgroundBlur =
        Config.Preference.Background.WallpaperBlurRadius;

    [ObservableProperty] private bool _backgroundBlurVisible;

    [ObservableProperty] private bool _backgroundClearEnabled = true;

    [ObservableProperty] private bool _backgroundClearVisible;

    private int _backgroundCount;

    [ObservableProperty] private int _backgroundOpacity =
        Config.Preference.Background.WallpaperOpacity;

    [ObservableProperty] private bool _backgroundOpacityVisible;

    [ObservableProperty] private int _backgroundSuitSelectedIndex;

    [ObservableProperty] private bool _backgroundSuitVisible;

    [ObservableProperty] private string _customNetText = string.Empty;

    [ObservableProperty] private int _customPresetSelectedIndex = -1;

    [ObservableProperty] private int _customThemeHue = Config.Preference.Theme.WindowHue;

    [ObservableProperty] private int _customThemeSaturation = Config.Preference.Theme.WindowSat;

    [ObservableProperty] private int _customThemeLightness = Config.Preference.Theme.WindowLight;

    [ObservableProperty] private int _customThemeHueDelta = Config.Preference.Theme.WindowDelta;

    [ObservableProperty] private bool _customThemeVisible;

    [ObservableProperty] private int _darkColorSelectedIndex = -1;

    [ObservableProperty] private int _darkModeSelectedIndex = -1;

    [ObservableProperty] private bool _featureHideVisible = true;

    private bool _forceShowFeatureHide;
    private bool _hasBackground;
    private bool _hasMusic;
    [ObservableProperty] private bool _launcherHideVisible;

    [ObservableProperty] private int _launcherOpacity = Config.Preference.Theme.WindowOpacity;

    [ObservableProperty] private int _lightColorSelectedIndex = -1;

    [ObservableProperty] private string _logoText = string.Empty;

    [ObservableProperty] private bool _musicClearEnabled = true;

    [ObservableProperty] private bool _musicClearVisible;

    private int _musicCount;

    [ObservableProperty] private bool _musicDetailVisible;

    [ObservableProperty] private int _musicVolume = Config.Preference.Music.Volume;

    private IReadOnlyList<ColorTheme> _themeColorOptions = [];
    [ObservableProperty] private bool _musicVolumeVisible;

    private bool _reloading;

    public string BackgroundTitle =>
        _hasBackground
            ? Lang.Text("Setup.Ui.Background.TitleWithCount", _backgroundCount)
            : Lang.Text("Setup.Ui.Background.TitleDefault");

    public string FeatureHideTitle =>
        _forceShowFeatureHide
            ? Lang.Text("Setup.Ui.FeatureHide.TitleTemporarilyDisabled")
            : Lang.Text("Setup.Ui.FeatureHide.Title");

    public string MusicTitle =>
        _hasMusic
            ? Lang.Text("Setup.Ui.Music.TitleWithCount", _musicCount)
            : Lang.Text("Setup.Ui.Music.Title");

    partial void OnDarkModeSelectedIndexChanged(int value)
    {
        if (!_reloading && value >= 0)
            Config.Preference.Theme.ColorMode = (ColorMode)value;
    }

    partial void OnLightColorSelectedIndexChanged(int value)
    {
        if (!_reloading && value >= 0)
            Config.Preference.Theme.LightColor = GetThemeAt(value);
        RefreshCustomThemeVisibility();
    }

    partial void OnDarkColorSelectedIndexChanged(int value)
    {
        if (!_reloading && value >= 0)
            Config.Preference.Theme.DarkColor = GetThemeAt(value);
        RefreshCustomThemeVisibility();
    }

    partial void OnCustomThemeHueChanged(int value)
    {
        if (!_reloading) Config.Preference.Theme.WindowHue = Math.Clamp(value, 0, 359);
    }

    partial void OnCustomThemeSaturationChanged(int value)
    {
        if (!_reloading) Config.Preference.Theme.WindowSat = Math.Clamp(value, 0, 100);
    }

    partial void OnCustomThemeLightnessChanged(int value)
    {
        if (!_reloading) Config.Preference.Theme.WindowLight = Math.Clamp(value, 0, 40);
    }

    partial void OnCustomThemeHueDeltaChanged(int value)
    {
        if (!_reloading) Config.Preference.Theme.WindowDelta = Math.Clamp(value, 0, 180);
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

    partial void OnMusicVolumeChanged(int value)
    {
        if (!_reloading) Config.Preference.Music.Volume = value;
    }

    public void Reload(IReadOnlyList<ColorTheme>? themeColorOptions = null)
    {
        _reloading = true;
        try
        {
            if (themeColorOptions is not null)
                _themeColorOptions = themeColorOptions;

            var theme = Config.Preference.Theme;
            LauncherOpacity = theme.WindowOpacity;
            DarkModeSelectedIndex = Math.Clamp((int)theme.ColorMode, 0, 2);
            LightColorSelectedIndex = FindThemeIndex(theme.LightColor);
            DarkColorSelectedIndex = FindThemeIndex(theme.DarkColor);
            CustomThemeHue = Math.Clamp(theme.WindowHue, 0, 359);
            CustomThemeSaturation = Math.Clamp(theme.WindowSat, 0, 100);
            CustomThemeLightness = Math.Clamp(theme.WindowLight, 0, 40);
            CustomThemeHueDelta = Math.Clamp(theme.WindowDelta, 0, 180);

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

        RefreshCustomThemeVisibility();
    }

    private ColorTheme GetThemeAt(int index)
    {
        if ((uint)index < (uint)_themeColorOptions.Count)
            return _themeColorOptions[index];
        return ColorTheme.CatBlue;
    }

    private int FindThemeIndex(ColorTheme theme)
    {
        for (var i = 0; i < _themeColorOptions.Count; i++)
            if (_themeColorOptions[i] == theme)
                return i;
        for (var i = 0; i < _themeColorOptions.Count; i++)
            if (_themeColorOptions[i] == ColorTheme.CatBlue)
                return i;
        return 0;
    }

    private void RefreshCustomThemeVisibility()
    {
        CustomThemeVisible = !OperatingSystem.IsWindows()
            && (Config.Preference.Theme.LightColor == ColorTheme.Custom
                || Config.Preference.Theme.DarkColor == ColorTheme.Custom);
    }

    public void ShowLauncherHint()
    {
        LauncherHideVisible = OperatingSystem.IsWindows();
    }

    public void ApplyBackgroundState(bool show, int count)
    {
        BackgroundOpacityVisible = show;
        BackgroundBlurVisible = show;
        BackgroundSuitVisible = show;
        BackgroundClearVisible = show;
        _hasBackground = show;
        _backgroundCount = count;
        OnPropertyChanged(nameof(BackgroundTitle));
    }

    public void ApplyMusicState(bool hasMusic, int count)
    {
        MusicVolumeVisible = hasMusic;
        MusicDetailVisible = hasMusic;
        MusicClearVisible = hasMusic;
        _hasMusic = hasMusic;
        _musicCount = count;
        OnPropertyChanged(nameof(MusicTitle));
    }

    public void ApplyFeatureHideState(bool forceShow, bool functionHidden)
    {
        _forceShowFeatureHide = forceShow;
        FeatureHideVisible = forceShow || !functionHidden;
        OnPropertyChanged(nameof(FeatureHideTitle));
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

    /// <summary>统计目录中的背景图片数量；只统计图片扩展名。</summary>
    public static int CountContentFiles(string directory)
    {
        if (!Directory.Exists(directory)) return 0;
        try
        {
            string[] extensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif"];
            return Directory
                .EnumerateFiles(directory)
                .Count(file => extensions.Contains(
                    Path.GetExtension(file),
                    StringComparer.OrdinalIgnoreCase));
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
}
