// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;

#pragma warning disable CA1822, CS0067

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupLauncherMisc : MyPageRight, ISettingsPageInteractionSource
{
    public PageSetupLauncherMisc()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = PanBack;
        LauncherSettingsPageBinder.Attach(this);
    }

    public event EventHandler<SettingsPathRequestedEventArgs>? OpenPathRequested;

    public event EventHandler<SettingsUrlRequestedEventArgs>? OpenUrlRequested;

    public event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;

    private void ApplyHttpProxyBtn_OnClicked(object? sender, EventArgs e)
    {
        MessageRequested?.Invoke(
            this,
            new SettingsMessageRequestedEventArgs(
                "代理设置已保存",
                "新的网络请求会优先使用这里保存的代理设置；已经开始的下载或登录请求不会被中断。"));
    }

    private async void BtnSystemSettingExp_Click(object? sender, EventArgs e)
    {
        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("导出失败", "当前窗口无法打开保存对话框。"));
            return;
        }

        IStorageFile? target = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出启动器设置",
            SuggestedFileName = "PCL-N-Settings.json",
            FileTypeChoices =
            [
                new FilePickerFileType("JSON 配置文件") { Patterns = ["*.json"] }
            ]
        }).ConfigureAwait(true);
        if (target is null)
            return;

        try
        {
            string sourcePath = LauncherSettingsPageBinder.CreateSettingsPath();
            LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
            LauncherSettingsPageBinder.SaveSettings(settings);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("设置文件尚未生成。");

            await using Stream source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            await using Stream destination = await target.OpenWriteAsync().ConfigureAwait(true);
            destination.SetLength(0);
            await source.CopyToAsync(destination).ConfigureAwait(true);
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("导出完成", "启动器设置已导出到：\n" + target.Path.LocalPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException)
        {
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("导出失败", "未能导出启动器设置。\n\n详细信息：" + ex.Message));
        }
    }

    private async void BtnSystemSettingImp_Click(object? sender, EventArgs e)
    {
        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("导入失败", "当前窗口无法打开文件选择器。"));
            return;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入启动器设置",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JSON 配置文件") { Patterns = ["*.json"] }
            ]
        }).ConfigureAwait(true);
        if (files.Count == 0)
            return;

        string sourcePath = files[0].Path.LocalPath;
        ConfirmRequested?.Invoke(
            this,
            new SettingsConfirmRequestedEventArgs(
                "导入启动器设置",
                "导入后会覆盖当前启动器设置。建议先导出现有设置作为备份。\n\n确定继续吗？",
                confirmed =>
                {
                    if (!confirmed)
                        return;

                    try
                    {
                        using LauncherSettingsStore sourceStore = new(sourcePath);
                        LauncherSettingsLoadResult result = sourceStore.LoadAsync().AsTask().GetAwaiter().GetResult();
                        if (result.RecoveredFromInvalidFile)
                            throw new InvalidDataException("选择的文件不是有效的 PCL N 设置文件。");

                        LauncherSettingsPageBinder.SaveSettings(result.Settings);
                        MessageRequested?.Invoke(
                            this,
                            new SettingsMessageRequestedEventArgs(
                                "导入完成",
                                "设置已导入。部分界面选项会在重新打开页面后刷新，少数系统设置需要重启启动器后生效。"));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
                    {
                        MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("导入失败", "未能导入启动器设置。\n\n详细信息：" + ex.Message));
                    }
                },
                primaryButton: "导入",
                isWarn: true));
    }

    private void CheckBoxChange(object sender, bool user)
    {
    }

    private void CheckDebugMode_OnChange(object sender, bool user)
    {
    }

    private void CheckSystemDisableHardwareAcceleration_OnChange(object sender, bool user)
    {
    }

    private void ComboSystemActivity_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
    }

    private void RadioBoxChange(object sender, RouteEventArgs e)
    {
    }

    private void SliderChange(object sender, bool user)
    {
    }
}
