// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.UI.Abstractions.Pages;

namespace PCL.UI.Abstractions.Navigation;

public sealed record NavigationPageDescriptor
{
    public required NavigationRouteId Route { get; init; }

    public required string Title { get; init; }

    public string? Icon { get; init; }

    public int Order { get; init; }

    public PageRegion Region { get; init; } = PageRegion.Main;

    public required IPageProvider Provider { get; init; }
}

public interface INavigationRegistry
{
    event EventHandler? Changed;

    IReadOnlyList<NavigationPageDescriptor> Pages { get; }

    void AddPage(NavigationPageDescriptor descriptor);

    bool RemovePage(NavigationRouteId route);

    bool ReplacePage(NavigationRouteId route, NavigationPageDescriptor descriptor);
}

public sealed class NavigationRegistry : INavigationRegistry
{
    private readonly List<NavigationPageDescriptor> _pages = [];
    private readonly Dictionary<string, NavigationPageDescriptor> _pageMap = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<NavigationPageDescriptor> _snapshot = Array.Empty<NavigationPageDescriptor>();

    public IReadOnlyList<NavigationPageDescriptor> Pages => _snapshot;

    public event EventHandler? Changed;

    public void AddPage(NavigationPageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ValidateDescriptor(descriptor);
        if (!_pageMap.TryAdd(descriptor.Route.Value, descriptor))
            throw new InvalidOperationException($"导航路由已注册：{descriptor.Route}");

        _pages.Add(descriptor);
        RefreshSnapshot();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool RemovePage(NavigationRouteId route)
    {
        if (route.IsEmpty || !_pageMap.Remove(route.Value))
            return false;

        int index = FindRoute(route);
        if (index < 0)
            return false;

        _pages.RemoveAt(index);
        RefreshSnapshot();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool ReplacePage(NavigationRouteId route, NavigationPageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ValidateDescriptor(descriptor);
        if (route.IsEmpty || !_pageMap.ContainsKey(route.Value))
            return false;
        if (!descriptor.Route.Equals(route.Value) && _pageMap.ContainsKey(descriptor.Route.Value))
            throw new InvalidOperationException($"导航路由已注册：{descriptor.Route}");

        int index = FindRoute(route);
        if (index < 0)
            return false;

        _pageMap.Remove(route.Value);
        _pageMap[descriptor.Route.Value] = descriptor;
        _pages[index] = descriptor;
        RefreshSnapshot();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private int FindRoute(NavigationRouteId route) =>
        _pages.FindIndex(page => page.Route.Equals(route.Value));

    private static void ValidateDescriptor(NavigationPageDescriptor descriptor)
    {
        if (descriptor.Route.IsEmpty)
            throw new ArgumentException("导航路由不能为空。", nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.Title))
            throw new ArgumentException("导航标题不能为空。", nameof(descriptor));
    }

    private void RefreshSnapshot() =>
        _snapshot = _pages
            .OrderBy(static page => page.Order)
            .ThenBy(static page => page.Route.Value, StringComparer.Ordinal)
            .ToArray();
}
