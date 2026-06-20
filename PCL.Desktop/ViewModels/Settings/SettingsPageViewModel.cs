// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Windows.Input;
using PCL.Application.Settings;
using PCL.Core.App;
using PCL.UI.Abstractions;

namespace PCL.Desktop.ViewModels.Settings;

public sealed record ColorModeOption(string Name, ColorMode Value);

public sealed record ColorThemeOption(string Name, ColorTheme Value);

public sealed record DownloadSourceOption(
    string Name,
    DownloadSourcePreference Value);

public sealed class SettingsPageViewModel : ObservableObject, IDisposable
{
    private readonly IHintService _hintService;
    private readonly IThemeService _themeService;
    private readonly LauncherSettingsStore _store;
    private bool _automaticallyRepairGameIssues = true;
    private ColorModeOption _selectedColorMode;
    private ColorThemeOption _selectedColorTheme;
    private DownloadSourceOption _selectedDownloadSource;
    private bool _isBusy;

    public SettingsPageViewModel(
        LauncherSettingsStore store,
        IThemeService themeService,
        IHintService hintService)
    {
        _store = store;
        _themeService = themeService;
        _hintService = hintService;
        ColorModes =
        [
            new("跟随系统", ColorMode.System),
            new("亮色", ColorMode.Light),
            new("暗色", ColorMode.Dark)
        ];
        ColorThemes =
        [
            new("龙猫蓝", ColorTheme.CatBlue),
            new("天蓝", ColorTheme.SkyBlue),
            new("跟随系统主题色", ColorTheme.SystemAccent)
        ];
        DownloadSources =
        [
            new(
                "优先官方源，失败时使用镜像",
                DownloadSourcePreference.PreferOfficialWithMirrorFallback),
            new("仅使用官方源", DownloadSourcePreference.OfficialOnly),
            new("仅使用镜像源", DownloadSourcePreference.MirrorOnly)
        ];
        _selectedColorMode = ColorModes[0];
        _selectedColorTheme = ColorThemes[0];
        _selectedDownloadSource = DownloadSources[0];
        SaveCommand = new AsyncDelegateCommand(SaveAsync);
        ResetCommand = new AsyncDelegateCommand(ResetAsync);
    }

    public IReadOnlyList<ColorModeOption> ColorModes { get; }

    public IReadOnlyList<ColorThemeOption> ColorThemes { get; }

    public IReadOnlyList<DownloadSourceOption> DownloadSources { get; }

    public ICommand SaveCommand { get; }

    public ICommand ResetCommand { get; }

    private static bool SupportsSystemAccentTheme =>
        !OperatingSystem.IsWindows();

    public bool AutomaticallyRepairGameIssues
    {
        get => _automaticallyRepairGameIssues;
        set => SetProperty(ref _automaticallyRepairGameIssues, value);
    }

    public ColorModeOption SelectedColorMode
    {
        get => _selectedColorMode;
        set => SetProperty(ref _selectedColorMode, value);
    }

    public ColorThemeOption SelectedColorTheme
    {
        get => _selectedColorTheme;
        set
        {
            if (OperatingSystem.IsWindows() &&
                value.Value == ColorTheme.SystemAccent)
            {
                _hintService.ShowWarning(
                    "Windows 上不支持跟随系统主题色，已切回龙猫蓝。");
                value = ColorThemes.Single(
                    static option => option.Value == ColorTheme.CatBlue);
            }

            SetProperty(ref _selectedColorTheme, value);
        }
    }

    public DownloadSourceOption SelectedDownloadSource
    {
        get => _selectedDownloadSource;
        set => SetProperty(ref _selectedDownloadSource, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            LauncherSettingsLoadResult result =
                await _store.LoadAsync(cancellationToken);
            ApplySettings(LauncherSettingsPolicy.Normalize(
                result.Settings,
                supportsSystemAccentTheme: SupportsSystemAccentTheme,
                allowsDomesticMirror: true));
            if (result.RecoveredFromInvalidFile)
            {
                _hintService.ShowWarning(
                    "设置文件损坏，已恢复默认设置并保留原文件备份。");
            }
        }
        catch
        {
            _hintService.ShowError("读取启动器设置失败，已使用默认设置。");
            ApplySettings(new LauncherSettings());
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose() => _store.Dispose();

    public async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            LauncherSettings settings = BuildSettings();
            await _store.SaveAsync(settings);
            ApplyTheme(settings);
            _hintService.ShowSuccess("设置已保存。");
        }
        catch
        {
            _hintService.ShowError("保存设置失败，请检查数据目录是否可写。");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResetAsync()
    {
        ApplySettings(new LauncherSettings());
        await SaveAsync();
    }

    private LauncherSettings BuildSettings() =>
        LauncherSettingsPolicy.Normalize(
            new LauncherSettings
            {
                AutomaticallyRepairGameIssues =
                    AutomaticallyRepairGameIssues,
                ColorMode = SelectedColorMode.Value,
                LightColor = SelectedColorTheme.Value,
                DarkColor = SelectedColorTheme.Value,
                DownloadSource = SelectedDownloadSource.Value
            },
            supportsSystemAccentTheme: SupportsSystemAccentTheme,
            allowsDomesticMirror: true);

    private void ApplySettings(LauncherSettings settings)
    {
        AutomaticallyRepairGameIssues =
            settings.AutomaticallyRepairGameIssues;
        SelectedColorMode = ColorModes.Single(
            option => option.Value == settings.ColorMode);
        SelectedColorTheme = ColorThemes.Single(
            option => option.Value == settings.LightColor);
        SelectedDownloadSource = DownloadSources.Single(
            option => option.Value == settings.DownloadSource);
        ApplyTheme(settings);
    }

    private void ApplyTheme(LauncherSettings settings)
    {
        ThemeMode mode = settings.ColorMode switch
        {
            ColorMode.Light => ThemeMode.Light,
            ColorMode.Dark => ThemeMode.Dark,
            _ => ThemeMode.System
        };
        AccentColor accent = settings.LightColor switch
        {
            ColorTheme.SkyBlue => AccentColor.SkyBlue,
            ColorTheme.SystemAccent => AccentColor.System,
            _ => AccentColor.CatBlue
        };
        _themeService.Apply(mode, accent);
    }
}
