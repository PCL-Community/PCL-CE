// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.UI.Abstractions.Themes;

public sealed record ThemeDescriptor
{
    public required ThemeId Id { get; init; }

    public required string DisplayName { get; init; }

    public bool IsPlatformRestricted { get; init; }

    public int Order { get; init; }
}

public interface IThemeRegistry
{
    IReadOnlyList<ThemeDescriptor> Themes { get; }

    void AddTheme(ThemeDescriptor descriptor);

    bool RemoveTheme(ThemeId id);
}

public sealed class ThemeRegistry : IThemeRegistry
{
    private readonly List<ThemeDescriptor> _themes = [];
    private readonly Dictionary<string, ThemeDescriptor> _themeMap = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<ThemeDescriptor> _snapshot = Array.Empty<ThemeDescriptor>();

    public IReadOnlyList<ThemeDescriptor> Themes => _snapshot;

    public void AddTheme(ThemeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(descriptor.Id.Value))
            throw new ArgumentException("主题 ID 不能为空。", nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
            throw new ArgumentException("主题名称不能为空。", nameof(descriptor));
        if (!_themeMap.TryAdd(descriptor.Id.Value, descriptor))
            throw new InvalidOperationException($"主题已注册：{descriptor.Id}");

        _themes.Add(descriptor);
        RefreshSnapshot();
    }

    public bool RemoveTheme(ThemeId id)
    {
        if (string.IsNullOrWhiteSpace(id.Value) || !_themeMap.Remove(id.Value))
            return false;

        int index = _themes.FindIndex(theme => theme.Id.Equals(id.Value));
        if (index < 0)
            return false;

        _themes.RemoveAt(index);
        RefreshSnapshot();
        return true;
    }

    private void RefreshSnapshot() =>
        _snapshot = _themes
            .OrderBy(static theme => theme.Order)
            .ThenBy(static theme => theme.Id.Value, StringComparer.Ordinal)
            .ToArray();
}
