// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PCL.Application.Hosting.PluginPlatform;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Settings.Views;

internal sealed class PageSetupPluginInstalled : PluginSettingsPageBase
{
    private readonly TextBlock _statusLabel = CreateMutedText("正在读取插件目录……");
    private readonly StackPanel _pluginList = new() { Spacing = 8 };

    public PageSetupPluginInstalled(HostSettingsPageDescriptor descriptor)
        : base(descriptor)
    {
        AddHeaderCard();
        MyCard card = CreateCard("插件列表");
        StackPanel content = CreateCardContent(spacing: 10d);
        content.Children.Add(_statusLabel);

        WrapPanel actions = CreateButtonWrap();
        MyButton installButton = CreateActionButton("安装 .pnp", 110d);
        installButton.Click += async (_, _) => await InstallPackageAsync().ConfigureAwait(true);
        MyButton refreshButton = CreateActionButton("刷新", 80d);
        refreshButton.Click += (_, _) => RefreshPage();
        actions.Children.Add(installButton);
        actions.Children.Add(refreshButton);
        content.Children.Add(actions);

        content.Children.Add(CreateSectionTitle("已安装"));
        content.Children.Add(_pluginList);
        card.Children.Add(content);
        PanMain.Children.Add(card);
        RefreshPage();
    }

    public override void RefreshPage()
    {
        if (!TryGetCatalog(out IPluginCatalogService? catalog) || catalog is null)
        {
            _statusLabel.Text = "插件目录未初始化。";
            SetUnavailable(_pluginList);
            return;
        }

        try
        {
            IReadOnlyList<PluginCatalogEntry> entries = catalog.ListInstalled();
            PluginSafetySettings safety = catalog.Safety;
            _statusLabel.Text =
                $"运行时：{catalog.RootPath} · 已安装 {entries.Count} · " +
                $"PluginSafe={(safety.PluginSafeMode ? "开" : "关")} · UiSafe={(safety.UiSafeMode ? "开" : "关")} · {FormatMarketState(catalog)}";

            _pluginList.Children.Clear();
            if (entries.Count == 0)
            {
                _pluginList.Children.Add(CreateMutedText("尚未安装第三方插件。可在此安装 .pnp，或前往“市场”扫描本地市场目录。", 13d));
                return;
            }

            foreach (PluginCatalogEntry entry in entries)
                _pluginList.Children.Add(CreatePluginRow(entry));
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "刷新失败：" + ex.Message;
        }
    }

    private Border CreatePluginRow(PluginCatalogEntry entry)
    {
        Border border = CreateRowBorder();
        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        StackPanel text = new() { Spacing = 2 };
        text.Children.Add(new TextBlock
        {
            Text = entry.Name,
            FontWeight = FontWeight.SemiBold,
            FontSize = 14d
        });
        text.Children.Add(new TextBlock
        {
            Text = $"{entry.PluginId} · v{entry.ActiveVersion ?? "—"} · {entry.StatusMessage}",
            FontSize = 12d,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap
        });
        if (entry.RequiredDependencies.Count > 0 || entry.MissingPrerequisites.Count > 0)
        {
            string depLine = entry.MissingPrerequisites.Count > 0
                ? "前置缺失: " + string.Join(", ", entry.MissingPrerequisites)
                : "前置: " + string.Join(", ", entry.RequiredDependencies);
            text.Children.Add(new TextBlock
            {
                Text = entry.DependencyState is null ? depLine : entry.DependencyState + " · " + depLine,
                FontSize = 11d,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
                Foreground = entry.MissingPrerequisites.Count > 0
                    ? new SolidColorBrush(Color.FromRgb(180, 90, 40))
                    : null
            });
        }

        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        MyButton toggle = new()
        {
            Text = entry.IsEnabled ? "禁用" : "启用",
            MinWidth = 72,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        string pluginId = entry.PluginId;
        bool enable = !entry.IsEnabled;
        toggle.Click += async (_, _) =>
        {
            toggle.IsEnabled = false;
            try
            {
                await PluginCatalogAccess.Current.SetEnabledAsync(pluginId, enable).ConfigureAwait(true);
                ShowInfo(enable ? $"已启用插件 {pluginId}" : $"已禁用插件 {pluginId}");
            }
            catch (Exception ex)
            {
                ShowWarning("操作失败：" + ex.Message);
            }
            finally
            {
                RefreshPage();
            }
        };
        Grid.SetColumn(toggle, 1);
        grid.Children.Add(toggle);

        border.Child = grid;
        return border;
    }

    private async Task InstallPackageAsync()
    {
        if (!TryGetCatalog(out IPluginCatalogService? catalog) || catalog is null)
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            ShowWarning("无法打开文件选择器。");
            return;
        }

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "选择 PCL N 插件包 (.pnp)",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("PCL N Plugin")
                    {
                        Patterns = ["*.pnp"]
                    }
                ]
            }).ConfigureAwait(true);

        if (files.Count == 0)
            return;

        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            ShowWarning("无法读取所选文件路径。");
            return;
        }

        try
        {
            PluginCatalogEntry entry = await catalog.InstallPackageAsync(path).ConfigureAwait(true);
            ShowInfo($"已安装 {entry.Name} ({entry.PluginId} {entry.ActiveVersion})");
        }
        catch (Exception ex)
        {
            ShowWarning("安装失败：" + ex.Message);
        }
        finally
        {
            RefreshPage();
        }
    }

    private static MyButton CreateActionButton(string text, double minWidth) =>
        new()
        {
            Text = text,
            MinWidth = minWidth,
            Height = 32d,
            Margin = new Thickness(0d, 0d, 8d, 6d)
        };
}
