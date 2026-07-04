// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Downloads.Views;

public enum DownloadPageSubType
{
    Install = 0,
    Progress = 1
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
    private readonly Func<PageDownloadInstall> _installFactory;
    private readonly Func<PageDownloadProgress> _progressFactory;
    private PageDownloadInstall? _installPage;
    private PageDownloadProgress? _progressPage;
    private bool _isLoadedOnce;

    public PageDownloadLeft()
        : this(() => new PageDownloadInstall(), () => new PageDownloadProgress())
    {
    }

    public PageDownloadLeft(Func<PageDownloadInstall> installFactory, Func<PageDownloadProgress> progressFactory)
    {
        _installFactory = installFactory;
        _progressFactory = progressFactory;
        AvaloniaXamlLoader.Load(this);
        AnimatedControl = this.FindControl<Control>("PanItem");
        AttachedToVisualTree += (_, _) =>
        {
            if (_isLoadedOnce)
                return;

            _isLoadedOnce = true;
            this.FindControl<MyListItem>("ItemAll")?.SetChecked(true, user: false, animate: false);
            ApplyCurrentFilter();
        };
    }

    public event EventHandler<DownloadPageChangedEventArgs>? PageChanged;

    public DownloadPageSubType PageId { get; private set; } = DownloadPageSubType.Install;

    public DownloadVersionFilter VersionFilter { get; private set; } = DownloadVersionFilter.All;

    public MyPageRight GetOrCreateCurrentPage() => PageGet(PageId);

    public MyPageRight PageGet(DownloadPageSubType page) =>
        page switch
        {
            DownloadPageSubType.Progress => _progressPage ??= _progressFactory(),
            _ => _installPage ??= _installFactory()
        };

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

        VersionFilter = item.Tag switch
        {
            string text when int.TryParse(text, out int value) => ToVersionFilter(value),
            int id => ToVersionFilter(id),
            _ => DownloadVersionFilter.All
        };
        ApplyCurrentFilter();
        PageChange(DownloadPageSubType.Install);
    }

    private static DownloadVersionFilter ToVersionFilter(int value) =>
        Enum.IsDefined(typeof(DownloadVersionFilter), value)
            ? (DownloadVersionFilter)value
            : DownloadVersionFilter.All;
}
