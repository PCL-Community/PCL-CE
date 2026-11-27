using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Theme;
using PCL.Core.UI.Theming;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>界面个性化设置页的视图模型。</summary>
public partial class PageSetupUIViewModel : ViewModelBase
{
    private int _backgroundCount;

    [ObservableProperty] private bool _darkCustomVisible;

    private bool _forceShowFeatureHide;
    private bool _hasBackground;
    private bool _hasMusic;

    [ObservableProperty] private bool _lightCustomVisible;

    private int _musicCount;

    private bool _reloading;

    private IReadOnlyList<ColorTheme> _themeColorOptions = [];

    [ObservableProperty]
    public partial int BackgroundBlur { get; set; } =
        Config.Preference.Background.WallpaperBlurRadius;

    [ObservableProperty] public partial bool BackgroundBlurVisible { get; set; }

    [ObservableProperty] public partial bool BackgroundClearEnabled { get; set; } = true;

    [ObservableProperty] public partial bool BackgroundClearVisible { get; set; }

    [ObservableProperty]
    public partial int BackgroundOpacity { get; set; } =
        Config.Preference.Background.WallpaperOpacity;

    [ObservableProperty] public partial bool BackgroundOpacityVisible { get; set; }

    [ObservableProperty] public partial int BackgroundSuitSelectedIndex { get; set; }

    [ObservableProperty] public partial bool BackgroundSuitVisible { get; set; }

    [ObservableProperty]
    public partial int BlurRadius { get; set; } = Config.Preference.Blur.Radius;

    [ObservableProperty] public partial string CustomNetText { get; set; } = string.Empty;

    [ObservableProperty] public partial int CustomPresetSelectedIndex { get; set; } = -1;

    [ObservableProperty] public partial int DarkColorSelectedIndex { get; set; } = -1;

    [ObservableProperty] public partial Color DarkCustomColor { get; set; } = Colors.DodgerBlue;

    [ObservableProperty] public partial int DarkHueSpread { get; set; }

    [ObservableProperty] public partial ThemePalette DarkPalette { get; set; } = ThemePalette.Empty;

    [ObservableProperty] public partial bool FeatureHideVisible { get; set; } = true;

    [ObservableProperty] public partial bool LauncherHideVisible { get; set; }

    [ObservableProperty]
    public partial int LauncherOpacity { get; set; } = Config.Preference.Theme.WindowOpacity;

    [ObservableProperty] public partial int LightColorSelectedIndex { get; set; } = -1;

    [ObservableProperty] public partial Color LightCustomColor { get; set; } = Colors.DodgerBlue;

    [ObservableProperty] public partial int LightHueSpread { get; set; }

    [ObservableProperty]
    public partial ThemePalette LightPalette { get; set; } = ThemePalette.Empty;

    [ObservableProperty] public partial string LogoText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool MusicClearEnabled { get; set; } = true;

    [ObservableProperty] public partial bool MusicClearVisible { get; set; }

    [ObservableProperty] public partial bool MusicDetailVisible { get; set; }

    [ObservableProperty]
    public partial int MusicVolume { get; set; } = Config.Preference.Music.Volume;

    [ObservableProperty] public partial bool MusicVolumeVisible { get; set; }

    [ObservableProperty] public partial bool ShowDarkMode { get; set; }

    [ObservableProperty] public partial bool ShowLightMode { get; set; } = true;

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

    partial void OnLightColorSelectedIndexChanged(int value)
    {
        if (_reloading)
            return;
        if (value >= 0)
            Config.Preference.Theme.LightColor = GetThemeAt(value);
        RefreshLightCustomVisibility();
        if (GetThemeAt(value) == ColorTheme.Custom)
            LoadLightCustom();
        RefreshLightPalette();
    }

    partial void OnDarkColorSelectedIndexChanged(int value)
    {
        if (_reloading)
            return;
        if (value >= 0)
            Config.Preference.Theme.DarkColor = GetThemeAt(value);
        RefreshDarkCustomVisibility();
        if (GetThemeAt(value) == ColorTheme.Custom)
            LoadDarkCustom();
        RefreshDarkPalette();
    }

    public void ApplyLightCustomColor(Color value)
    {
        var current = ThemeService.GetCustomColor(false);
        var updated = ThemeCustomColor.FromSrgb(
            value.R,
            value.G,
            value.B,
            ThemeService.ToneProfiles.Light,
            current.HueSpread,
            current.Hue);
        ThemeService.SetCustomColor(false, updated);
        RefreshLightPalette();
    }

    public void ApplyDarkCustomColor(Color value)
    {
        var current = ThemeService.GetCustomColor(true);
        var updated = ThemeCustomColor.FromSrgb(
            value.R,
            value.G,
            value.B,
            ThemeService.ToneProfiles.Dark,
            current.HueSpread,
            current.Hue);
        ThemeService.SetCustomColor(true, updated);
        RefreshDarkPalette();
    }

    public void ApplyLightHueSpread(int value)
    {
        var updated = (ThemeService.GetCustomColor(false) with
        {
            HueSpread = Math.Clamp(value, -90, 90)
        }).Normalize();
        ThemeService.SetCustomColor(false, updated);
        LightHueSpread = updated.HueSpread;
    }

    public void ApplyDarkHueSpread(int value)
    {
        var updated = (ThemeService.GetCustomColor(true) with
        {
            HueSpread = Math.Clamp(value, -90, 90)
        }).Normalize();
        ThemeService.SetCustomColor(true, updated);
        DarkHueSpread = updated.HueSpread;
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
            LightColorSelectedIndex = FindThemeIndex(theme.LightColor);
            DarkColorSelectedIndex = FindThemeIndex(theme.DarkColor);
            LoadLightCustom();
            LoadDarkCustom();
            BlurRadius = Config.Preference.Blur.Radius;

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

        RefreshLightCustomVisibility();
        RefreshDarkCustomVisibility();
        RefreshLightPalette();
        RefreshDarkPalette();
        RefreshVisibleMode();
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

    private void RefreshLightCustomVisibility()
    {
        LightCustomVisible = ThemeService.SupportsCustomThemes
            && GetThemeAt(LightColorSelectedIndex) == ColorTheme.Custom;
    }

    private void RefreshDarkCustomVisibility()
    {
        DarkCustomVisible = ThemeService.SupportsCustomThemes
            && GetThemeAt(DarkColorSelectedIndex) == ColorTheme.Custom;
    }

    private void LoadLightCustom()
    {
        var custom = ThemeService.GetCustomColor(false);
        LightHueSpread = custom.HueSpread;
        LightCustomColor = custom
            .ToReferenceColor(ThemeService.ToneProfiles.Light)
            .ToAvaloniaColor();
    }

    private void LoadDarkCustom()
    {
        var custom = ThemeService.GetCustomColor(true);
        DarkHueSpread = custom.HueSpread;
        DarkCustomColor = custom
            .ToReferenceColor(ThemeService.ToneProfiles.Dark)
            .ToAvaloniaColor();
    }

    private void RefreshLightPalette()
    {
        LightPalette = ThemePaletteBuilder.Build(false, GetThemeAt(LightColorSelectedIndex));
    }

    private void RefreshDarkPalette()
    {
        DarkPalette = ThemePaletteBuilder.Build(true, GetThemeAt(DarkColorSelectedIndex));
    }

    public void RefreshVisibleMode()
    {
        var mode = Config.Preference.Theme.ColorMode;
        var dark = mode switch
        {
            ColorMode.Dark => true,
            ColorMode.System => ThemeService.SystemIsDarkMode,
            _ => false
        };

        if (dark)
        {
            ShowLightMode = false;
            ShowDarkMode = true;
        }
        else
        {
            ShowDarkMode = false;
            ShowLightMode = true;
        }
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