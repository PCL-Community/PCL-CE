// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Community;

public partial class PageCommunityLeft : MyPageLeft, IRefreshable
{
    private static readonly CommunityCategoryDescriptor[] Categories =
    [
        new(CommunityResourceCategory.Mod, "Mod", "lucide/puzzle"),
        new(CommunityResourceCategory.Modpack, "整合包", "lucide/package"),
        new(CommunityResourceCategory.DataPack, "数据包", "lucide/boxes"),
        new(CommunityResourceCategory.ResourcePack, "资源包", "lucide/layers"),
        new(CommunityResourceCategory.Shader, "光影包", "lucide/sparkles"),
        new(CommunityResourceCategory.World, "世界", "lucide/globe")
    ];

    public PageCommunityLeft()
    {
        AvaloniaXamlLoader.Load(this);
        AnimatedControl = this.FindControl<Control>("PanItem");
        ReloadItems();
    }

    public CommunityResourceCategory Category { get; private set; } = CommunityResourceCategory.Mod;

    public event EventHandler<CommunityResourceCategory>? CategoryChanged;

    public event EventHandler<CommunityResourceCategory>? RefreshRequested;

    public bool TrySelectCategory(CommunityResourceCategory category)
    {
        if (!Categories.Any(descriptor => descriptor.Category == category))
            return false;

        if (Category == category)
            return true;

        Category = category;
        SyncChecks();
        CategoryChanged?.Invoke(this, category);
        return true;
    }

    public void Refresh() => RefreshRequested?.Invoke(this, Category);

    private void ReloadItems()
    {
        if (this.FindControl<StackPanel>("PanItem") is not { } panel)
            return;

        panel.Children.Clear();
        panel.Children.Add(new TextBlock
        {
            Text = "社区资源",
            Margin = new Thickness(13d, 10d, 5d, 4d),
            Opacity = 0.6d,
            FontSize = 12d
        });
        foreach (CommunityCategoryDescriptor descriptor in Categories)
            panel.Children.Add(CreateCategoryItem(descriptor));
        SyncChecks();
    }

    private MyListItem CreateCategoryItem(CommunityCategoryDescriptor descriptor)
    {
        MyIconButton refresh = new()
        {
            SvgIcon = "lucide/refresh-cw",
            LogoScale = 0.85d,
            ToolTip = "刷新"
        };
        refresh.Click += (_, _) => RefreshRequested?.Invoke(this, descriptor.Category);

        MyListItem item = new()
        {
            Title = descriptor.Title,
            SvgIcon = descriptor.Icon,
            LogoScale = 0.95d,
            Height = 36d,
            MinPaddingRight = 35d,
            Type = MyListItem.CheckType.RadioBox,
            IsScaleAnimationEnabled = false,
            Tag = descriptor.Category,
            Buttons = [refresh]
        };
        item.Click += (_, _) => TrySelectCategory(descriptor.Category);
        return item;
    }

    private void SyncChecks()
    {
        if (this.FindControl<StackPanel>("PanItem") is not { } panel)
            return;

        foreach (MyListItem item in panel.Children.OfType<MyListItem>())
        {
            if (item.Tag is CommunityResourceCategory category)
                item.SetChecked(category == Category, user: false, animate: false);
        }
    }

    private sealed record CommunityCategoryDescriptor(
        CommunityResourceCategory Category,
        string Title,
        string Icon);
}
