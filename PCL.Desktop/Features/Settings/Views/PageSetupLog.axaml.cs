// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PCL.Desktop.Controls.Legacy;

#pragma warning disable CA1822, CS0067

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupLog : MyPageRight, IRefreshableSettingsPage, ISettingsPageInteractionSource
{
    public PageSetupLog()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = PanBack;
        AttachedToVisualTree += (_, _) => RefreshPage();
    }

    public event EventHandler<SettingsPathRequestedEventArgs>? OpenPathRequested;

    public event EventHandler<SettingsUrlRequestedEventArgs>? OpenUrlRequested;

    public event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;

    public void RefreshPage()
    {
        if (PanList is null)
            return;

        Directory.CreateDirectory(GetLogDirectory());
        PanList.Children.Clear();
        FileInfo[] logs = EnumerateLogs().ToArray();
        if (logs.Length == 0)
        {
            PanList.Children.Add(new MyHint
            {
                Text = "当前还没有可导出的日志文件。",
                Theme = MyHint.Themes.Blue
            });
            return;
        }

        foreach (FileInfo log in logs)
        {
            MyIconButton open = new()
            {
                SvgIcon = "lucide/external-link",
                ToolTip = "打开日志文件"
            };
            open.Click += (_, _) => OpenPathRequested?.Invoke(this, new SettingsPathRequestedEventArgs(log.FullName));
            PanList.Children.Add(new MyListItem
            {
                Title = log.Name,
                Info = $"{FormatSize(log.Length)}，{log.LastWriteTime:yyyy-MM-dd HH:mm:ss}",
                Height = 45d,
                Type = MyListItem.CheckType.Clickable,
                SvgIcon = "lucide/file-text",
                LogoScale = 0.9d,
                Buttons = [open]
            });
        }
    }

    private void ButtonClean_OnClick(object? sender, EventArgs e)
    {
        ConfirmRequested?.Invoke(
            this,
            new SettingsConfirmRequestedEventArgs(
                "清理日志",
                "确定要清理历史日志吗？最近的一份日志会被保留，方便继续排查当前问题。",
                confirmed =>
                {
                    if (!confirmed)
                        return;

                    try
                    {
                        FileInfo? latest = EnumerateLogs().FirstOrDefault();
                        foreach (FileInfo log in EnumerateLogs().Where(log => !string.Equals(log.FullName, latest?.FullName, StringComparison.OrdinalIgnoreCase)))
                            log.Delete();

                        RefreshPage();
                        MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("清理完成", "历史日志已清理。"));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("清理失败", "未能清理日志。\n\n详细信息：" + ex.Message));
                    }
                },
                primaryButton: "清理",
                isWarn: true));
    }

    private async void ButtonExportAll_OnClick(object? sender, EventArgs e)
    {
        await ExportLogsAsync(EnumerateLogs(), "PCLN-AllLogs").ConfigureAwait(true);
    }

    private async void ButtonExport_OnClick(object? sender, EventArgs e)
    {
        FileInfo? latest = EnumerateLogs().FirstOrDefault();
        await ExportLogsAsync(latest is null ? [] : [latest], "PCLN-CurrentLog").ConfigureAwait(true);
    }

    private void ButtonOpenDir_OnClick(object? sender, EventArgs e)
    {
        string directory = GetLogDirectory();
        Directory.CreateDirectory(directory);
        OpenPathRequested?.Invoke(this, new SettingsPathRequestedEventArgs(directory));
    }

    private async Task ExportLogsAsync(IEnumerable<FileInfo> logs, string namePrefix)
    {
        FileInfo[] files = logs.Where(static file => file.Exists).ToArray();
        if (files.Length == 0)
        {
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("没有日志", "当前没有可导出的日志文件。"));
            return;
        }

        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("导出失败", "当前窗口无法打开保存对话框。"));
            return;
        }

        IStorageFile? target = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出日志",
            SuggestedFileName = $"{namePrefix}-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            FileTypeChoices =
            [
                new FilePickerFileType("ZIP 压缩包") { Patterns = ["*.zip"] }
            ]
        }).ConfigureAwait(true);
        if (target is null)
            return;

        string targetPath = target.Path.LocalPath;
        try
        {
            await using FileStream targetStream = new(
                targetPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            using ZipArchive archive = new(targetStream, ZipArchiveMode.Create);
            foreach (FileInfo file in files)
            {
                ZipArchiveEntry entry = archive.CreateEntry(file.Name, CompressionLevel.Fastest);
                await using Stream entryStream = entry.Open();
                await using FileStream source = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                await source.CopyToAsync(entryStream).ConfigureAwait(true);
            }

            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("导出完成", "日志已导出到：\n" + targetPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("导出失败", "未能导出日志。\n\n详细信息：" + ex.Message));
        }
    }

    private static FileInfo[] EnumerateLogs()
    {
        DirectoryInfo directory = new(GetLogDirectory());
        if (!directory.Exists)
            return [];

        return directory.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Where(static file => file.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
                                  file.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(static file => file.LastWriteTimeUtc)
            .ToArray();
    }

    private static string GetLogDirectory() =>
        Path.Combine(LauncherSettingsPageBinder.CreateDataDirectory(), "Logs");

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }
}
