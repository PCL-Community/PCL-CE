// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.UI.Abstractions.Pages;

namespace PCL.UI.Abstractions.Navigation;

public sealed record NavigationPageDescriptor
{
    public required string Route { get; init; }

    public required string Title { get; init; }

    public string? Icon { get; init; }

    public int Order { get; init; }

    public PageRegion Region { get; init; } = PageRegion.Main;

    public required IPageProvider Provider { get; init; }
}

public interface INavigationRegistry
{
    IReadOnlyList<NavigationPageDescriptor> Pages { get; }

    void AddPage(NavigationPageDescriptor descriptor);

    bool RemovePage(string route);

    bool ReplacePage(string route, NavigationPageDescriptor descriptor);
}

public sealed class NavigationRegistry : INavigationRegistry
{
    private readonly List<NavigationPageDescriptor> _pages = [];

    public IReadOnlyList<NavigationPageDescriptor> Pages =>
        _pages
            .OrderBy(static page => page.Order)
            .ThenBy(static page => page.Route, StringComparer.Ordinal)
            .ToArray();

    public void AddPage(NavigationPageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ValidateDescriptor(descriptor);
        if (_pages.Any(page => string.Equals(page.Route, descriptor.Route, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"导航路由已注册：{descriptor.Route}");

        _pages.Add(descriptor);
    }

    public bool RemovePage(string route)
    {
        int index = FindRoute(route);
        if (index < 0)
            return false;

        _pages.RemoveAt(index);
        return true;
    }

    public bool ReplacePage(string route, NavigationPageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ValidateDescriptor(descriptor);
        int index = FindRoute(route);
        if (index < 0)
            return false;

        _pages[index] = descriptor;
        return true;
    }

    private int FindRoute(string route) =>
        _pages.FindIndex(page => string.Equals(page.Route, route, StringComparison.OrdinalIgnoreCase));

    private static void ValidateDescriptor(NavigationPageDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.Route))
            throw new ArgumentException("导航路由不能为空。", nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.Title))
            throw new ArgumentException("导航标题不能为空。", nameof(descriptor));
    }
}
