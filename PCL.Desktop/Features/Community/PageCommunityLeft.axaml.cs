// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

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
    }

    public CommunityResourceCategory Category { get; private set; } = CommunityResourceCategory.Mod;

    public event EventHandler<CommunityResourceCategory>? CategoryChanged;

    public event EventHandler<CommunityResourceCategory>? RefreshRequested;

    public bool TrySelectCategory(CommunityResourceCategory category)
    {
        if (Category == category)
        {
            SyncChecks();
            return true;
        }

        Category = category;
        SyncChecks();
        CategoryChanged?.Invoke(this, category);
        return true;
    }

    public void Refresh() => RefreshRequested?.Invoke(this, Category);

    private void PageCheck(object senderRaw, RouteEventArgs e)
    {
        if (senderRaw is not MyListItem item)
            return;

        CommunityResourceCategory category = ParseTag(item.Tag);
        if (Category == category)
            return;

        Category = category;
        SyncChecks();
        CategoryChanged?.Invoke(this, category);
    }

    private void AttachRefreshButtons()
    {
        foreach (MyListItem item in GetCategoryItems())
        {
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
            item.SetChecked(ParseTag(item.Tag) == Category, user: false, animate: false);
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
