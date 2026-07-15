// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Hosting;

namespace PCL.Desktop.Features.Community;

/// <summary>
/// Left rail for community resources — layout mirrors WPF <c>PageCommunityLeft</c>.
/// </summary>
public partial class PageCommunityLeft : MyPageLeft, IRefreshable
{
    public PageCommunityLeft()
    {
        AvaloniaXamlLoader.Load(this);
        AnimatedControl = this.FindControl<Control>("PanItem");
        AttachRefreshButtons();
        SyncChecks();
        AttachedToVisualTree += (_, _) =>
        {
            if (this.FindControl<StackPanel>("PanItem") is { } panel)
                DesktopHostUiComposition.Instance.RegisterSlot("pcl.page.community", "categories.after", panel);
        };
        DetachedFromVisualTree += (_, _) =>
            DesktopHostUiComposition.Instance.UnregisterSlot("pcl.page.community", "categories.after");
    }

    public CommunityResourceCategory Category { get; private set; } = CommunityResourceCategory.Mod;

    public bool IsFavoritesSelected { get; private set; }

    public event EventHandler<CommunityResourceCategory>? CategoryChanged;

    public event EventHandler<CommunityResourceCategory>? RefreshRequested;

    public event EventHandler? FavoritesRequested;

    public bool TrySelectCategory(CommunityResourceCategory category)
    {
        if (!IsFavoritesSelected && Category == category)
        {
            SyncChecks();
            return true;
        }

        Category = category;
        IsFavoritesSelected = false;
        SyncChecks();
        CategoryChanged?.Invoke(this, category);
        return true;
    }

    public void Refresh() => RefreshRequested?.Invoke(this, Category);

    private void PageCheck(object senderRaw, RouteEventArgs e)
    {
        if (senderRaw is not MyListItem item)
            return;

        if (string.Equals(item.Tag as string, "Favorites", StringComparison.Ordinal))
        {
            if (IsFavoritesSelected)
                return;
            IsFavoritesSelected = true;
            SyncChecks();
            FavoritesRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        CommunityResourceCategory category = ParseTag(item.Tag);
        if (!IsFavoritesSelected && Category == category)
            return;

        Category = category;
        IsFavoritesSelected = false;
        SyncChecks();
        CategoryChanged?.Invoke(this, category);
    }

    private void AttachRefreshButtons()
    {
        foreach (MyListItem item in GetCategoryItems())
        {
            if (string.Equals(item.Tag as string, "Favorites", StringComparison.Ordinal))
                continue;

            MyIconButton refresh = new()
            {
                SvgIcon = "lucide/refresh-cw",
                LogoScale = 0.85d,
                ToolTip = "刷新"
            };
            CommunityResourceCategory category = ParseTag(item.Tag);
            refresh.Click += (_, _) =>
            {
                TrySelectCategory(category);
                RefreshRequested?.Invoke(this, category);
            };
            item.Buttons = [refresh];
        }
    }

    private void SyncChecks()
    {
        foreach (MyListItem item in GetCategoryItems())
        {
            bool isFavorite = string.Equals(item.Tag as string, "Favorites", StringComparison.Ordinal);
            item.SetChecked(isFavorite ? IsFavoritesSelected : !IsFavoritesSelected && ParseTag(item.Tag) == Category,
                user: false,
                animate: false);
        }
    }

    private IEnumerable<MyListItem> GetCategoryItems()
    {
        if (this.FindControl<StackPanel>("PanItem") is not { } panel)
            yield break;

        foreach (Control child in panel.Children)
        {
            if (child is MyListItem item && item.Name is not null && item.Name.StartsWith("Item", StringComparison.Ordinal))
                yield return item;
        }
    }

    private static CommunityResourceCategory ParseTag(object? tag) =>
        tag switch
        {
            CommunityResourceCategory category => category,
            "Modpack" => CommunityResourceCategory.Modpack,
            "DataPack" => CommunityResourceCategory.DataPack,
            "ResourcePack" => CommunityResourceCategory.ResourcePack,
            "Shader" => CommunityResourceCategory.Shader,
            "World" => CommunityResourceCategory.World,
            _ => CommunityResourceCategory.Mod
        };
}
