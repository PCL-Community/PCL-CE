// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Community;

public partial class PageCommunityFavoritesRight : MyPageRight
{
    private readonly CommunityFavoritesStore _store;

    public PageCommunityFavoritesRight()
        : this(new CommunityFavoritesStore())
    {
    }

    public PageCommunityFavoritesRight(CommunityFavoritesStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        _store.Changed += Store_Changed;
        DetachedFromVisualTree += (_, _) => _store.Changed -= Store_Changed;
        Render();
    }

    public event EventHandler<CommunityFavoriteEntry>? OpenProjectRequested;

    public event EventHandler<CommunityResourceDownloadRequest>? DownloadRequested;

    public void Refresh() => Render();

    private void Store_Changed(object? sender, EventArgs e) => Render();

    private void Render()
    {
        if (this.FindControl<StackPanel>("PanFavorites") is not { } panel)
            return;
        panel.Children.Clear();
        IReadOnlyList<CommunityFavoriteEntry> favorites = _store.Items;
        if (favorites.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "还没有收藏社区资源。可在资源列表或详情页点击星标添加。",
                Margin = new Thickness(8, 14),
                Opacity = 0.7d,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });
            return;
        }

        foreach (CommunityFavoriteEntry favorite in favorites)
            panel.Children.Add(CreateItem(favorite));
        ControlVisualHelpers.AnimateListEntrance(panel, "Community Favorites");
    }

    private MyListItem CreateItem(CommunityFavoriteEntry favorite)
    {
        MyIconButton remove = new()
        {
            SvgIcon = "lucide/star-off",
            ToolTip = "取消收藏",
            Width = 25,
            Height = 25,
            Margin = new Thickness(0, 0, 4, 0)
        };
        remove.Click += (_, _) => _store.Toggle(favorite.Entry, favorite.Category);

        MyIconButton download = new()
        {
            SvgIcon = "lucide/download",
            ToolTip = "下载到当前实例",
            Width = 25,
            Height = 25
        };
        download.Click += (_, _) => DownloadRequested?.Invoke(
            this,
            new CommunityResourceDownloadRequest(
                favorite.Entry,
                favorite.Category,
                new CommunitySearchOptions(Source: favorite.Entry.Source)));

        MyListItem item = new()
        {
            Title = favorite.Entry.Title,
            Info = favorite.Entry.Description + "  ·  " +
                   (favorite.Entry.Source == CommunityResourceSource.CurseForge ? "CurseForge" : "Modrinth"),
            Height = 64d,
            Type = MyListItem.CheckType.Clickable,
            Tag = favorite,
            Logo = favorite.Entry.IconUrl ?? string.Empty,
            SvgIcon = string.IsNullOrWhiteSpace(favorite.Entry.IconUrl) ? "lucide/package" : string.Empty,
            LogoScale = 1.05d,
            Buttons = [download, remove]
        };
        item.Click += (_, _) => OpenProjectRequested?.Invoke(this, favorite);
        return item;
    }
}
