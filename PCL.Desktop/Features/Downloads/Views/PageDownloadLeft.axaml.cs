// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Hosting;

namespace PCL.Desktop.Features.Downloads.Views;

public enum DownloadPageSubType
{
    Install = 0
}

public enum DownloadVersionFilter
{
    All = 0,
    Release = 1,
    Snapshot = 2,
    BeforeRelease = 3,
    AprilFools = 4
}

public sealed class DownloadPageChangedEventArgs(DownloadPageSubType pageId, MyPageRight page) : EventArgs
{
    public DownloadPageSubType PageId { get; } = pageId;

    public MyPageRight Page { get; } = page;
}

public partial class PageDownloadLeft : MyPageLeft
{
    private readonly DownloadPageFactoryContext _pageContext;
    private bool _isLoadedOnce;

    public PageDownloadLeft()
        : this(null)
    {
    }

    public PageDownloadLeft(Func<PageDownloadInstall>? installFactory)
    {
        _pageContext = new DownloadPageFactoryContext(installFactory);
        AvaloniaXamlLoader.Load(this);
        AnimatedControl = this.FindControl<Control>("PanItem");
        InitializeFilterItems();
        AttachedToVisualTree += (_, _) =>
        {
            if (this.FindControl<StackPanel>("PanItem") is { } panel)
                DesktopHostUiComposition.Instance.RegisterSlot("pcl.page.download", "filters.after", panel);
            if (_isLoadedOnce)
                return;

            _isLoadedOnce = true;
            this.FindControl<MyListItem>("ItemAll")?.SetChecked(true, user: false, animate: false);
            ApplyCurrentFilter();
        };
        DetachedFromVisualTree += (_, _) =>
            DesktopHostUiComposition.Instance.UnregisterSlot("pcl.page.download", "filters.after");
    }

    public event EventHandler<DownloadPageChangedEventArgs>? PageChanged;

    public DownloadPageSubType PageId { get; private set; } = DownloadPageSubType.Install;

    public DownloadVersionFilter VersionFilter { get; private set; } = DownloadVersionFilter.All;

    public MyPageRight GetOrCreateCurrentPage() => PageGet(PageId);

    public MyPageRight PageGet(DownloadPageSubType page) =>
        DownloadPageRegistry.CreatePage(page, _pageContext);

    public void PageChange(DownloadPageSubType page, bool force = false)
    {
        if (!force && PageId == page)
            return;

        PageId = page;
        PageChanged?.Invoke(this, new DownloadPageChangedEventArgs(page, PageGet(page)));
    }

    public void ApplyCurrentFilter()
    {
        if (PageGet(DownloadPageSubType.Install) is PageDownloadInstall installPage)
            installPage.ApplyVersionFilter(VersionFilter);
    }

    private void PageCheck(object senderRaw, RouteEventArgs e)
    {
        if (senderRaw is not MyListItem item)
            return;

        VersionFilter = ToVersionFilter(item.Tag);
        ApplyCurrentFilter();
        PageChange(DownloadPageSubType.Install);
    }

    private void InitializeFilterItems()
    {
        foreach (DownloadVersionFilterDescriptor descriptor in DownloadVersionFilterRegistry.Items)
        {
            if (this.FindControl<MyListItem>(descriptor.ItemName) is { } item)
                item.Tag = descriptor.Filter;
        }
    }

    private static DownloadVersionFilter ToVersionFilter(object? tag) =>
        tag switch
        {
            DownloadVersionFilter filter => DownloadVersionFilterRegistry.Normalize(filter),
            string text when int.TryParse(text, out int value) => DownloadVersionFilterRegistry.Normalize(value),
            int value => DownloadVersionFilterRegistry.Normalize(value),
            _ => DownloadVersionFilter.All
        };
}
