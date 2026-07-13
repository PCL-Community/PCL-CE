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

internal sealed class PageSetupPluginMarket : PluginSettingsPageBase
{
    private readonly TextBlock _statusLabel = CreateMutedText("尚未扫描本地市场目录。");
    private readonly StackPanel _marketList = new() { Spacing = 8 };

    public PageSetupPluginMarket(HostSettingsPageDescriptor descriptor)
        : base(descriptor)
    {
        AddHeaderCard();
        MyCard card = CreateCard("本地市场");
        StackPanel content = CreateCardContent(spacing: 10d);
        content.Children.Add(_statusLabel);

        WrapPanel actions = CreateButtonWrap();
        MyButton marketButton = CreateActionButton("扫描本地市场", 120d);
        marketButton.Click += async (_, _) => await BrowseLocalMarketAsync().ConfigureAwait(true);
        MyButton refreshButton = CreateActionButton("刷新状态", 90d);
        refreshButton.Click += (_, _) => RefreshPage();
        actions.Children.Add(marketButton);
        actions.Children.Add(refreshButton);
        content.Children.Add(actions);

        content.Children.Add(_marketList);
        card.Children.Add(content);
        PanMain.Children.Add(card);
        RefreshPage();
    }

    public override void RefreshPage()
    {
        if (!TryGetCatalog(out IPluginCatalogService? catalog) || catalog is null)
        {
            _statusLabel.Text = "插件目录未初始化。";
            SetUnavailable(_marketList);
            return;
        }

        _statusLabel.Text = FormatMarketState(catalog);
        if (_marketList.Children.Count == 0)
            _marketList.Children.Add(CreateMutedText("选择本地插件市场目录后，将在此显示扫描到的 .pnp 包。", 13d));
    }

    private Border CreateMarketRow(PluginMarketListing listing)
    {
        Border border = CreateRowBorder();
        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        string title = listing.Name ?? Path.GetFileName(listing.PackagePath);
        string detail = listing.Error is not null
            ? $"{listing.PluginId ?? "?"} · {listing.Error}"
            : $"{listing.PluginId ?? "?"} · v{listing.Version ?? "—"} · {(listing.CanInspect ? "签名校验通过" : "仅元数据")}";

        StackPanel text = new() { Spacing = 2 };
        text.Children.Add(new TextBlock { Text = title, FontWeight = Avalonia.Media.FontWeight.SemiBold, FontSize = 14d });
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
            // The package installer requires a fully inspectable (including signature-valid) package.
            // Do not offer an action that is guaranteed to fail for metadata-only scan results.
            IsEnabled = listing.Error is null && listing.CanInspect
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
        };
        Grid.SetColumn(install, 1);
        grid.Children.Add(install);

        border.Child = grid;
        return border;
    }

    private async Task BrowseLocalMarketAsync()
    {
        if (!TryGetCatalog(out IPluginCatalogService? catalog) || catalog is null)
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            ShowWarning("无法打开文件夹选择器。");
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
            ShowWarning("无法读取所选目录路径。");
            return;
        }

        try
        {
            IReadOnlyList<PluginMarketListing> listings = catalog.BrowseLocalMarket(path);
            _marketList.Children.Clear();
            if (listings.Count == 0)
            {
                _marketList.Children.Add(CreateMutedText($"目录中未找到 .pnp：{path}", 13d));
            }
            else
            {
                foreach (PluginMarketListing listing in listings)
                    _marketList.Children.Add(CreateMarketRow(listing));
                ShowInfo($"本地市场扫描到 {listings.Count} 个包");
            }
        }
        catch (Exception ex)
        {
            ShowWarning("扫描失败：" + ex.Message);
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
