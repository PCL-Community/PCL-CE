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
using PCL.Desktop.Hosting;

namespace PCL.Desktop.Features.Settings.Views;

/// <summary>
/// HostModule settings page. For <c>pcl.plugin.settings</c>, renders install/list/enable UI
/// when the embedded plugin catalog is available.
/// </summary>
public sealed class PageSetupHostModule : MyPageRight, IRefreshableSettingsPage
{
    private readonly HostSettingsPageDescriptor _descriptor;
    private readonly StackPanel _pluginList = new() { Spacing = 8 };
    private readonly TextBlock _statusLabel = new()
    {
        FontSize = 12d,
        Opacity = 0.75,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0d, 0d, 0d, 8d)
    };

    public PageSetupHostModule(HostSettingsPageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        _descriptor = descriptor;

        StackPanel panel = new() { Margin = new Thickness(25d, 25d, 25d, 10d) };
        StackPanel content = new() { Margin = new Thickness(25d, 40d, 25d, 20d) };
        content.Children.Add(new TextBlock
        {
            Name = "LabHostHeading",
            Text = descriptor.Heading,
            FontSize = 20d,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 0d, 0d, 10d)
        });
        content.Children.Add(new TextBlock
        {
            Name = "LabHostDescription",
            Text = descriptor.Description,
            FontSize = 13d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 0d, 0d, 12d)
        });
        foreach (HostSettingsHintDescriptor hint in descriptor.Hints)
        {
            content.Children.Add(new MyHint
            {
                Text = hint.Text,
                Theme = hint.Kind switch
                {
                    HostSettingsHintKind.Warning => MyHint.Themes.Yellow,
                    HostSettingsHintKind.Error => MyHint.Themes.Red,
                    _ => MyHint.Themes.Blue
                },
                Margin = new Thickness(0d, 6d, 0d, 0d)
            });
        }

        if (IsPluginManagementPage(descriptor) && PluginCatalogAccess.IsInitialized)
            content.Children.Add(BuildManagementPanel());
        else if (IsPluginManagementPage(descriptor))
        {
            content.Children.Add(new MyHint
            {
                Text = "当前构建未注入 PCL.Plugin 运行时；第三方 .pnp 管理不可用。使用 scripts/run-plugin-ui.ps1 可本地嵌入调试。",
                Theme = MyHint.Themes.Yellow,
                Margin = new Thickness(0d, 12d, 0d, 0d)
            });
        }

        MyCard card = new() { Title = descriptor.Title };
        card.Children.Add(content);
        panel.Children.Add(card);
        MyScrollViewer scroll = new()
        {
            Name = "PanBack",
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = panel
        };
        PanScroll = scroll;
        Content = scroll;

        if (IsPluginManagementPage(descriptor) && PluginCatalogAccess.IsInitialized)
            RefreshPage();
    }

    public void RefreshPage()
    {
        if (!PluginCatalogAccess.IsInitialized)
        {
            _statusLabel.Text = "插件目录未初始化。";
            _pluginList.Children.Clear();
            return;
        }

        try
        {
            IPluginCatalogService catalog = PluginCatalogAccess.Current;
            IReadOnlyList<PluginCatalogEntry> entries = catalog.ListInstalled();
            _statusLabel.Text = $"运行时目录：{catalog.RootPath} · 已安装 {entries.Count} 个插件";
            _pluginList.Children.Clear();
            if (entries.Count == 0)
            {
                _pluginList.Children.Add(new TextBlock
                {
                    Text = "尚未安装第三方插件。点击「安装 .pnp」选择已签名的插件包。",
                    FontSize = 13d,
                    Opacity = 0.8,
                    TextWrapping = TextWrapping.Wrap
                });
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

    private StackPanel BuildManagementPanel()
    {
        StackPanel panel = new() { Margin = new Thickness(0d, 16d, 0d, 0d), Spacing = 10 };
        panel.Children.Add(_statusLabel);

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        MyButton installButton = new() { Text = "安装 .pnp", MinWidth = 110 };
        installButton.Click += async (_, _) => await InstallPackageAsync().ConfigureAwait(true);
        MyButton refreshButton = new() { Text = "刷新", MinWidth = 80 };
        refreshButton.Click += (_, _) => RefreshPage();
        actions.Children.Add(installButton);
        actions.Children.Add(refreshButton);
        panel.Children.Add(actions);
        panel.Children.Add(_pluginList);
        return panel;
    }

    private Border CreatePluginRow(PluginCatalogEntry entry)
    {
        Border border = new()
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10)
        };

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
                DesktopPluginHostNotifications.Instance.ShowInformation(
                    enable ? $"已启用插件 {pluginId}" : $"已禁用插件 {pluginId}");
            }
            catch (Exception ex)
            {
                DesktopPluginHostNotifications.Instance.ShowWarning("操作失败：" + ex.Message);
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
        if (!PluginCatalogAccess.IsInitialized)
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            DesktopPluginHostNotifications.Instance.ShowWarning("无法打开文件选择器。");
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
            DesktopPluginHostNotifications.Instance.ShowWarning("无法读取所选文件路径。");
            return;
        }

        try
        {
            PluginCatalogEntry entry = await PluginCatalogAccess.Current
                .InstallPackageAsync(path)
                .ConfigureAwait(true);
            DesktopPluginHostNotifications.Instance.ShowInformation(
                $"已安装 {entry.Name} ({entry.PluginId} {entry.ActiveVersion})");
        }
        catch (Exception ex)
        {
            DesktopPluginHostNotifications.Instance.ShowWarning("安装失败：" + ex.Message);
        }
        finally
        {
            RefreshPage();
        }
    }

    private static bool IsPluginManagementPage(HostSettingsPageDescriptor descriptor) =>
        string.Equals(descriptor.Id, "pcl.plugin.settings", StringComparison.OrdinalIgnoreCase);
}
