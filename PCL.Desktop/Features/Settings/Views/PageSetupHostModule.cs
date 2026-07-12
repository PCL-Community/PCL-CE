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
/// HostModule settings page. For <c>pcl.plugin.settings</c>, renders install/list/enable UI,
/// Safe Mode toggles, and local-folder marketplace when the plugin runtime is embedded.
/// </summary>
public sealed class PageSetupHostModule : MyPageRight, IRefreshableSettingsPage
{
    private readonly HostSettingsPageDescriptor _descriptor;
    private readonly StackPanel _pluginList = new() { Spacing = 8 };
    private readonly StackPanel _marketList = new() { Spacing = 8 };
    private readonly StackPanel _conflictList = new() { Spacing = 8 };
    private readonly StackPanel _compatList = new() { Spacing = 6 };
    private readonly TextBlock _statusLabel = new()
    {
        FontSize = 12d,
        Opacity = 0.75,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0d, 0d, 0d, 8d)
    };
    private readonly TextBlock _patchStatusLabel = new()
    {
        FontSize = 12d,
        Opacity = 0.75,
        TextWrapping = TextWrapping.Wrap
    };
    private CheckBox? _pluginSafeModeBox;
    private CheckBox? _uiSafeModeBox;

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
            _marketList.Children.Clear();
            _conflictList.Children.Clear();
            _compatList.Children.Clear();
            return;
        }

        try
        {
            IPluginCatalogService catalog = PluginCatalogAccess.Current;
            PluginSafetySettings safety = catalog.Safety;
            if (_pluginSafeModeBox is not null)
                _pluginSafeModeBox.IsChecked = safety.PluginSafeMode;
            if (_uiSafeModeBox is not null)
                _uiSafeModeBox.IsChecked = safety.UiSafeMode;

            IReadOnlyList<PluginCatalogEntry> entries = catalog.ListInstalled();
            _statusLabel.Text =
                $"运行时：{catalog.RootPath} · 已安装 {entries.Count} · " +
                $"PluginSafe={(safety.PluginSafeMode ? "开" : "关")} · UiSafe={(safety.UiSafeMode ? "开" : "关")}";

            PluginUiPatchApplyResult apply = catalog.ApplyUiPatches();
            _patchStatusLabel.Text =
                $"UI Patch：逻辑应用 {apply.AppliedGlobalIds.Count} · 视觉应用 {apply.VisuallyAppliedGlobalIds.Count} · " +
                $"Safe 拦截 {apply.BlockedBySafeMode.Count} · 冲突拦截 {apply.BlockedByConflict.Count}";

            _pluginList.Children.Clear();
            if (entries.Count == 0)
            {
                _pluginList.Children.Add(new TextBlock
                {
                    Text = "尚未安装第三方插件。可「安装 .pnp」或「扫描本地市场目录」。",
                    FontSize = 13d,
                    Opacity = 0.8,
                    TextWrapping = TextWrapping.Wrap
                });
            }
            else
            {
                foreach (PluginCatalogEntry entry in entries)
                    _pluginList.Children.Add(CreatePluginRow(entry));
            }

            _conflictList.Children.Clear();
            PluginUiConflictSummary[] conflicts = apply.Conflicts
                .Where(static c => string.Equals(c.Severity, "Error", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (conflicts.Length == 0)
            {
                _conflictList.Children.Add(new TextBlock
                {
                    Text = "当前无阻塞性 UI 冲突。",
                    FontSize = 12d,
                    Opacity = 0.75
                });
            }
            else
            {
                foreach (PluginUiConflictSummary conflict in conflicts)
                    _conflictList.Children.Add(CreateConflictRow(conflict));
            }

            _compatList.Children.Clear();
            IReadOnlyList<PluginCompatibilityRecord> compat = catalog.ListCompatibility();
            if (compat.Count == 0)
            {
                _compatList.Children.Add(new TextBlock
                {
                    Text = "尚无本地兼容性观测记录。",
                    FontSize = 12d,
                    Opacity = 0.75
                });
            }
            else
            {
                foreach (PluginCompatibilityRecord record in compat.Take(8))
                {
                    _compatList.Children.Add(new TextBlock
                    {
                        Text = $"{record.PluginA} × {record.PluginB} @ {record.Target} → {record.Result}",
                        FontSize = 12d,
                        Opacity = 0.8,
                        TextWrapping = TextWrapping.Wrap
                    });
                }
            }
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
        panel.Children.Add(_patchStatusLabel);

        // Safe mode
        _pluginSafeModeBox = new CheckBox
        {
            Content = "Plugin Safe Mode（不加载第三方插件）",
            FontSize = 13d
        };
        _pluginSafeModeBox.IsCheckedChanged += (_, _) => PersistSafetyFromUi();
        _uiSafeModeBox = new CheckBox
        {
            Content = "UI Safe Mode（跳过 modify/replace/raw 类 Patch）",
            FontSize = 13d
        };
        _uiSafeModeBox.IsCheckedChanged += (_, _) => PersistSafetyFromUi();
        panel.Children.Add(_pluginSafeModeBox);
        panel.Children.Add(_uiSafeModeBox);

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        MyButton installButton = new() { Text = "安装 .pnp", MinWidth = 110 };
        installButton.Click += async (_, _) => await InstallPackageAsync().ConfigureAwait(true);
        MyButton marketButton = new() { Text = "扫描本地市场", MinWidth = 120 };
        marketButton.Click += async (_, _) => await BrowseLocalMarketAsync().ConfigureAwait(true);
        MyButton refreshButton = new() { Text = "刷新", MinWidth = 80 };
        refreshButton.Click += (_, _) => RefreshPage();
        MyButton applyPatchButton = new() { Text = "重算 UI Patch", MinWidth = 120 };
        applyPatchButton.Click += (_, _) =>
        {
            try
            {
                PluginUiPatchApplyResult result = PluginCatalogAccess.Current.ApplyUiPatches();
                DesktopPluginHostNotifications.Instance.ShowInformation(
                    $"UI Patch 已应用 {result.AppliedGlobalIds.Count} 项（Safe 拦截 {result.BlockedBySafeMode.Count}）");
            }
            catch (Exception ex)
            {
                DesktopPluginHostNotifications.Instance.ShowWarning(ex.Message);
            }
            finally
            {
                RefreshPage();
            }
        };
        actions.Children.Add(installButton);
        actions.Children.Add(marketButton);
        actions.Children.Add(applyPatchButton);
        actions.Children.Add(refreshButton);
        panel.Children.Add(actions);

        panel.Children.Add(new TextBlock
        {
            Text = "已安装",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 8, 0, 0)
        });
        panel.Children.Add(_pluginList);

        panel.Children.Add(new TextBlock
        {
            Text = "本地市场（最近一次扫描）",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 12, 0, 0)
        });
        panel.Children.Add(_marketList);

        panel.Children.Add(new TextBlock
        {
            Text = "UI 冲突决策",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 12, 0, 0)
        });
        panel.Children.Add(_conflictList);

        panel.Children.Add(new TextBlock
        {
            Text = "本地兼容性观测（最近）",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 12, 0, 0)
        });
        panel.Children.Add(_compatList);
        return panel;
    }

    private Border CreateConflictRow(PluginUiConflictSummary conflict)
    {
        Border border = new()
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 200, 120, 40)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10)
        };

        StackPanel stack = new() { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = $"{conflict.Kind} · {conflict.Target}",
            FontWeight = FontWeight.SemiBold,
            FontSize = 13d,
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"{conflict.LeftGlobalId}  vs  {conflict.RightGlobalId}\n{conflict.Message}" +
                   (conflict.Resolution is null ? string.Empty : $"\n已决策：{conflict.Resolution}"),
            FontSize = 12d,
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap
        });

        StackPanel buttons = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
        buttons.Children.Add(CreateConflictActionButton("禁用左侧", conflict, PluginConflictResolution.DisableLeft));
        buttons.Children.Add(CreateConflictActionButton("禁用右侧", conflict, PluginConflictResolution.DisableRight));
        buttons.Children.Add(CreateConflictActionButton("强制两者", conflict, PluginConflictResolution.ForceBoth));
        stack.Children.Add(buttons);

        border.Child = stack;
        return border;
    }

    private MyButton CreateConflictActionButton(
        string text,
        PluginUiConflictSummary conflict,
        PluginConflictResolution resolution)
    {
        MyButton button = new() { Text = text, MinWidth = 88, Height = 30 };
        string left = conflict.LeftGlobalId;
        string right = conflict.RightGlobalId;
        button.Click += (_, _) =>
        {
            try
            {
                PluginCatalogAccess.Current.ResolveUiConflict(left, right, resolution);
                DesktopPluginHostNotifications.Instance.ShowInformation($"冲突决策已保存：{resolution}");
            }
            catch (Exception ex)
            {
                DesktopPluginHostNotifications.Instance.ShowWarning(ex.Message);
            }
            finally
            {
                RefreshPage();
            }
        };
        return button;
    }

    private void PersistSafetyFromUi()
    {
        if (!PluginCatalogAccess.IsInitialized || _pluginSafeModeBox is null || _uiSafeModeBox is null)
            return;

        try
        {
            PluginCatalogAccess.Current.SetSafety(new PluginSafetySettings(
                _pluginSafeModeBox.IsChecked == true,
                _uiSafeModeBox.IsChecked == true));
            DesktopPluginHostNotifications.Instance.ShowInformation("安全模式设置已保存");
        }
        catch (Exception ex)
        {
            DesktopPluginHostNotifications.Instance.ShowWarning(ex.Message);
        }
        finally
        {
            RefreshPage();
        }
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

    private Border CreateMarketRow(PluginMarketListing listing)
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

        string title = listing.Name ?? Path.GetFileName(listing.PackagePath);
        string detail = listing.Error is not null
            ? $"{listing.PluginId ?? "?"} · {listing.Error}"
            : $"{listing.PluginId ?? "?"} · v{listing.Version ?? "—"} · {(listing.CanInspect ? "签名校验通过" : "仅元数据")}";

        StackPanel text = new() { Spacing = 2 };
        text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, FontSize = 14d });
        text.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = 12d,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(listing.Summary))
        {
            text.Children.Add(new TextBlock
            {
                Text = listing.Summary,
                FontSize = 12d,
                Opacity = 0.65,
                TextWrapping = TextWrapping.Wrap
            });
        }

        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        MyButton install = new()
        {
            Text = "安装",
            MinWidth = 72,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            IsEnabled = listing.Error is null || listing.PluginId is not null
        };
        string packagePath = listing.PackagePath;
        install.Click += async (_, _) =>
        {
            install.IsEnabled = false;
            try
            {
                PluginCatalogEntry entry = await PluginCatalogAccess.Current
                    .InstallPackageAsync(packagePath)
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
        };
        Grid.SetColumn(install, 1);
        grid.Children.Add(install);

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

    private async Task BrowseLocalMarketAsync()
    {
        if (!PluginCatalogAccess.IsInitialized)
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            DesktopPluginHostNotifications.Instance.ShowWarning("无法打开文件夹选择器。");
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "选择本地插件市场目录（扫描 *.pnp）",
                AllowMultiple = false
            }).ConfigureAwait(true);

        if (folders.Count == 0)
            return;

        string? path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            DesktopPluginHostNotifications.Instance.ShowWarning("无法读取所选目录路径。");
            return;
        }

        try
        {
            IReadOnlyList<PluginMarketListing> listings = PluginCatalogAccess.Current.BrowseLocalMarket(path);
            _marketList.Children.Clear();
            if (listings.Count == 0)
            {
                _marketList.Children.Add(new TextBlock
                {
                    Text = $"目录中未找到 .pnp：{path}",
                    FontSize = 13d,
                    Opacity = 0.8,
                    TextWrapping = TextWrapping.Wrap
                });
            }
            else
            {
                foreach (PluginMarketListing listing in listings)
                    _marketList.Children.Add(CreateMarketRow(listing));
                DesktopPluginHostNotifications.Instance.ShowInformation($"本地市场扫描到 {listings.Count} 个包");
            }
        }
        catch (Exception ex)
        {
            DesktopPluginHostNotifications.Instance.ShowWarning("扫描失败：" + ex.Message);
        }
    }

    private static bool IsPluginManagementPage(HostSettingsPageDescriptor descriptor) =>
        string.Equals(descriptor.Id, "pcl.plugin.settings", StringComparison.OrdinalIgnoreCase);
}
