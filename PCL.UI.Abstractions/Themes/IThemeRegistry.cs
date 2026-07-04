// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.UI.Abstractions.Themes;

public sealed record ThemeDescriptor
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public bool IsPlatformRestricted { get; init; }

    public int Order { get; init; }
}

public interface IThemeRegistry
{
    IReadOnlyList<ThemeDescriptor> Themes { get; }

    void AddTheme(ThemeDescriptor descriptor);

    bool RemoveTheme(string id);
}

public sealed class ThemeRegistry : IThemeRegistry
{
    private readonly List<ThemeDescriptor> _themes = [];

    public IReadOnlyList<ThemeDescriptor> Themes =>
        _themes
            .OrderBy(static theme => theme.Order)
            .ThenBy(static theme => theme.Id, StringComparer.Ordinal)
            .ToArray();

    public void AddTheme(ThemeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(descriptor.Id))
            throw new ArgumentException("主题 ID 不能为空。", nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
            throw new ArgumentException("主题名称不能为空。", nameof(descriptor));
        if (_themes.Any(theme => string.Equals(theme.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"主题已注册：{descriptor.Id}");

        _themes.Add(descriptor);
    }

    public bool RemoveTheme(string id)
    {
        int index = _themes.FindIndex(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return false;

        _themes.RemoveAt(index);
        return true;
    }
}
