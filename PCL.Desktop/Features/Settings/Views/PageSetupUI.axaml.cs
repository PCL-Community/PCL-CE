// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Theme;

#pragma warning disable CA1822, CS0067

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupUI : MyPageRight, IRefreshableSettingsPage, ISettingsPageInteractionSource
{
    public PageSetupUI()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = PanBack;
        LauncherSettingsPageBinder.Attach(this, settings =>
        {
            UiFontSelector.SelectedFontTag = settings.GetTextOption("UiFont");
            MotdFontSelector.SelectedFontTag = settings.GetTextOption("UiMotdFont");
        });
        InitializeSliderHints();
        AttachedToVisualTree += (_, _) => RefreshPage();
    }

    private void InitializeSliderHints()
    {
        if (this.FindControl<MySlider>("SliderMusicVolume") is { } musicVolume)
            musicVolume.getHintText = value =>
                (Math.Ceiling(value * 0.1d) / 100d).ToString("P0", CultureInfo.CurrentCulture);
        if (this.FindControl<MySlider>("SliderLauncherOpacity") is { } launcherOpacity)
            launcherOpacity.getHintText = value =>
                (Math.Round(40d + value * 0.1d) / 100d).ToString("P0", CultureInfo.CurrentCulture);
        if (this.FindControl<MySlider>("SliderBackgroundOpacity") is { } backgroundOpacity)
            backgroundOpacity.getHintText = value =>
                (Math.Round(value * 0.1d) / 100d).ToString("P0", CultureInfo.CurrentCulture);
        if (this.FindControl<MySlider>("SliderBackgroundBlur") is { } backgroundBlur)
            backgroundBlur.getHintText = value => value.ToString(CultureInfo.CurrentCulture) + " px";
        if (this.FindControl<MySlider>("SliderBlurValue") is { } blurValue)
            blurValue.getHintText = value => value.ToString(CultureInfo.CurrentCulture) + " px";
        if (this.FindControl<MySlider>("SliderBlurSamplingRate") is { } blurSampling)
            blurSampling.getHintText = value =>
                (value / 100d).ToString("P0", CultureInfo.CurrentCulture);
    }

    public event EventHandler<SettingsPathRequestedEventArgs>? OpenPathRequested;

    public event EventHandler<SettingsUrlRequestedEventArgs>? OpenUrlRequested;

    public event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;

    public void RefreshPage()
    {
        RefreshBackgroundUi(showMessage: false);
        RefreshMusicUi(showMessage: false);
        RefreshLogoUi();
        RefreshHomepageUi();
        RefreshBlurUi();
    }

    private void BtnBackgroundClear_Click(object? sender, EventArgs e)
    {
        ConfirmRequested?.Invoke(
            this,
            new SettingsConfirmRequestedEventArgs(
                "清空背景",
                "确定要删除背景目录中的所有文件吗？此操作不会影响其他启动器文件。",
                confirmed =>
                {
                    if (!confirmed)
                        return;

                    ClearDirectory(GetBackgroundDirectory());
                    RefreshBackgroundUi(showMessage: true);
                },
                primaryButton: "清空",
                isWarn: true));
    }

    private void BtnBackgroundRefresh_Click(object? sender, EventArgs e)
    {
        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        settings.SetTextOption("UiBackgroundRefreshToken", Guid.NewGuid().ToString("N"));
        LauncherSettingsPageBinder.SaveSettings(settings);
        RefreshBackgroundUi(showMessage: true);
    }

    private void BtnCustomRefresh_Click(object? sender, EventArgs e)
    {
        Directory.CreateDirectory(GetCustomHomepageDirectory());
        LauncherSettingsPageBinder.NotifySettingsChanged();
        MessageRequested?.Invoke(
            this,
            new SettingsMessageRequestedEventArgs(
                "主页文件已刷新",
                "自定义主页文件夹已准备好。修改本地主页文件后，重新打开启动页即可查看效果。"));
    }

    private void BtnCustomTutorial_Click(object? sender, EventArgs e)
    {
        OpenUrlRequested?.Invoke(this, new SettingsUrlRequestedEventArgs("https://github.com/MuXue1230-owo/PCL-N/wiki"));
    }

    private async void BtnLogoChange_Click(object? sender, EventArgs e)
    {
        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("无法更换图标", "当前窗口无法打开文件选择器。"));
            return;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择标题栏图标",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("图片文件")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.ico"],
                    MimeTypes = ["image/png", "image/jpeg", "image/webp", "image/x-icon"]
                }
            ]
        }).ConfigureAwait(true);
        if (files.Count == 0)
            return;

        string targetPath = GetCustomLogoPath();
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? LauncherSettingsPageBinder.CreateDataDirectory());
        await using (Stream source = await files[0].OpenReadAsync().ConfigureAwait(true))
        await using (FileStream destination = new(
                         targetPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 16 * 1024,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await source.CopyToAsync(destination).ConfigureAwait(true);
        }

        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        settings.SetTextOption(LauncherSettingKeys.UiCustomLogoPath, targetPath);
        LauncherSettingsPageBinder.SaveSettings(settings);
        RefreshLogoUi();
        MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("图标已更新", "自定义标题栏图标会在重新创建窗口后完整生效。"));
    }

    private void BtnLogoDelete_Click(object? sender, EventArgs e)
    {
        try
        {
            string logoPath = GetCustomLogoPath();
            if (File.Exists(logoPath))
                File.Delete(logoPath);

            LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
            settings.RemoveTextOption(LauncherSettingKeys.UiCustomLogoPath);
            LauncherSettingsPageBinder.SaveSettings(settings);
            RefreshLogoUi();
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("图标已清除", "已恢复默认标题栏图标。"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("清除失败", "未能清除自定义图标。\n\n详细信息：" + ex.Message));
        }
    }

    private void BtnMusicClear_Click(object? sender, EventArgs e)
    {
        ConfirmRequested?.Invoke(
            this,
            new SettingsConfirmRequestedEventArgs(
                "清空背景音乐",
                "确定要删除音乐目录中的所有文件吗？",
                confirmed =>
                {
                    if (!confirmed)
                        return;

                    ClearDirectory(GetMusicDirectory());
                    RefreshMusicUi(showMessage: true);
                },
                primaryButton: "清空",
                isWarn: true));
    }

    private void BtnMusicOpen_Click(object? sender, EventArgs e)
    {
        string directory = GetMusicDirectory();
        Directory.CreateDirectory(directory);
        OpenPathRequested?.Invoke(this, new SettingsPathRequestedEventArgs(directory));
    }

    private void BtnMusicRefresh_Click(object? sender, EventArgs e)
    {
        RefreshMusicUi(showMessage: true);
    }

    private void BtnUIBgOpen_Click(object? sender, EventArgs e)
    {
        string directory = GetBackgroundDirectory();
        Directory.CreateDirectory(directory);
        OpenPathRequested?.Invoke(this, new SettingsPathRequestedEventArgs(directory));
    }

    private void CheckBoxChange(object sender, bool user)
    {
        if (sender is MyCheckBox { Tag: "UiBlur" })
            RefreshBlurUi();
    }

    private void CheckMusicStart_OnChange(object sender, bool user)
    {
        if (user && sender is MyCheckBox { Checked: true } && CheckMusicStop is not null)
            CheckMusicStop.Checked = false;
    }

    private void CheckMusicStop_OnChange(object sender, bool user)
    {
        if (user && sender is MyCheckBox { Checked: true } && CheckMusicStart is not null)
            CheckMusicStart.Checked = false;
    }

    private void ComboChange(object? sender, SelectionChangedEventArgs e)
    {
    }

    private void ComboFontChange(object? sender, SelectionChangedEventArgs e)
    {
        SaveFontOption("UiFont", UiFontSelector.SelectedFontTag, applyTheme: true);
    }

    private void ComboMotdFontChange(object? sender, SelectionChangedEventArgs e)
    {
        SaveFontOption("UiMotdFont", MotdFontSelector.SelectedFontTag, applyTheme: false);
    }

    private void RadioBoxChange(object sender, RouteEventArgs e)
    {
        RefreshHomepageUi();
        RefreshLogoUi();
    }

    private void RadioLogoType3_Check(object sender, RouteEventArgs e)
    {
        RefreshLogoUi();
    }

    private void SliderChange(object sender, bool user)
    {
    }

    private void TextBoxChange(object? sender, TextChangedEventArgs e)
    {
    }

    private void ThemeColor_Change(object? sender, SelectionChangedEventArgs e)
    {
    }

    private static void SaveFontOption(string key, string? fontTag, bool applyTheme)
    {
        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        string normalized = fontTag?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalized))
            settings.TextOptions.Remove(key);
        else
            settings.SetTextOption(key, normalized);
        LauncherSettingsPageBinder.SaveSettings(settings);
        if (applyTheme)
            AvaloniaThemeManager.Apply(settings);
    }

    private FontSelector UiFontSelector => this.FindControl<FontSelector>("ComboUiFont")
        ?? throw new InvalidOperationException("PageSetupUI 缺少 ComboUiFont。");

    private FontSelector MotdFontSelector => this.FindControl<FontSelector>("ComboUiMotdFont")
        ?? throw new InvalidOperationException("PageSetupUI 缺少 ComboUiMotdFont。");

    private void RefreshBackgroundUi(bool showMessage)
    {
        int count = CountFiles(GetBackgroundDirectory(), "*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif", "*.mp4", "*.webm");
        bool hasContent = count > 0;
        if (PanBackgroundOpacity is not null)
            PanBackgroundOpacity.IsVisible = hasContent;
        if (PanBackgroundBlur is not null)
            PanBackgroundBlur.IsVisible = hasContent;
        if (PanBackgroundSuit is not null)
            PanBackgroundSuit.IsVisible = hasContent;
        if (BtnBackgroundClear is not null)
            BtnBackgroundClear.IsVisible = hasContent;
        if (CardBackground is not null)
            CardBackground.Title = hasContent ? $"背景图片与视频 ({count})" : "背景图片与视频";

        if (showMessage)
        {
            string message = hasContent
                ? $"已找到 {count} 个背景文件并立即应用。"
                : "背景目录中没有可用的图片或视频。";
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("背景已刷新", message));
        }

        LauncherSettingsPageBinder.NotifySettingsChanged();
    }

    private void RefreshMusicUi(bool showMessage)
    {
        int count = CountFiles(GetMusicDirectory(), "*.mp3", "*.wav", "*.flac", "*.ogg", "*.m4a");
        bool hasContent = count > 0;
        if (PanMusicVolume is not null)
            PanMusicVolume.IsVisible = hasContent;
        if (PanMusicDetail is not null)
            PanMusicDetail.IsVisible = hasContent;
        if (BtnMusicClear is not null)
            BtnMusicClear.IsVisible = hasContent;
        if (CardMusic is not null)
            CardMusic.Title = hasContent ? $"背景音乐 ({count})" : "背景音乐";

        if (showMessage)
        {
            string message = hasContent
                ? $"已找到 {count} 个音乐文件。"
                : "音乐目录中没有可播放的音频文件。";
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("音乐已刷新", message));
        }
    }

    private void RefreshLogoUi()
    {
        bool isCustomLogoSelected = RadioLogoType3?.Checked == true;
        if (PanLogoChange is not null)
            PanLogoChange.IsVisible = isCustomLogoSelected;
        if (PanLogoText is not null)
            PanLogoText.IsVisible = RadioLogoType2?.Checked == true;
        if (CheckLogoLeft is not null)
            CheckLogoLeft.IsVisible = RadioLogoType0?.Checked == true;
        if (BtnLogoDelete is not null)
            BtnLogoDelete.IsVisible = File.Exists(GetCustomLogoPath());
    }

    private void RefreshBlurUi()
    {
        if (this.FindControl<Grid>("PanBlurValue") is { } options)
            options.IsVisible = this.FindControl<MyCheckBox>("CheckBlur")?.Checked == true;
    }

    private void RefreshHomepageUi()
    {
        int selectedType = GetSelectedHomepageType();
        if (PanCustomLocal is not null)
            PanCustomLocal.IsVisible = selectedType == 1;
        if (PanCustomNet is not null)
            PanCustomNet.IsVisible = selectedType == 2;
        if (PanCustomPreset is not null)
            PanCustomPreset.IsVisible = selectedType == 3;
        if (HintCustomWarn is not null)
            HintCustomWarn.IsVisible = selectedType == 2;
    }

    private int GetSelectedHomepageType()
    {
        if (RadioCustomType1?.Checked == true)
            return 1;
        if (RadioCustomType2?.Checked == true)
            return 2;
        if (RadioCustomType3?.Checked == true)
            return 3;
        return 0;
    }

    private static string GetBackgroundDirectory() =>
        Path.Combine(LauncherSettingsPageBinder.CreateDataDirectory(), "Backgrounds");

    private static string GetMusicDirectory() =>
        Path.Combine(LauncherSettingsPageBinder.CreateDataDirectory(), "Musics");

    private static string GetCustomHomepageDirectory() =>
        Path.Combine(LauncherSettingsPageBinder.CreateDataDirectory(), "CustomHomepage");

    private static string GetCustomLogoPath() =>
        Path.Combine(LauncherSettingsPageBinder.CreateDataDirectory(), "Logo.png");

    private static int CountFiles(string directory, params string[] patterns)
    {
        if (!Directory.Exists(directory))
            return 0;

        return patterns
            .SelectMany(pattern => Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .Count();
    }

    private static void ClearDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            File.Delete(file);
    }
}
