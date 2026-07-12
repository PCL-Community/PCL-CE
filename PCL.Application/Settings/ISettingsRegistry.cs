// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Settings;

public sealed record SettingDescriptor(
    SettingKey Key,
    string Title,
    string? Description = null,
    object? DefaultValue = null);

public sealed record HostSettingsHintDescriptor(
    string Text,
    bool IsWarning = false);

public sealed record HostSettingsPageDescriptor(
    string Id,
    string Title,
    string Icon,
    string Heading,
    string Description,
    IReadOnlyList<HostSettingsHintDescriptor> Hints);

public interface ISettingsRegistry
{
    IReadOnlyList<SettingDescriptor> Settings { get; }

    void AddSetting(SettingDescriptor descriptor);

    bool RemoveSetting(SettingKey key);
}

public sealed class SettingsRegistry : ISettingsRegistry
{
    private readonly List<SettingDescriptor> _settings = [];
    private readonly Dictionary<string, SettingDescriptor> _settingMap = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<SettingDescriptor> _snapshot = Array.Empty<SettingDescriptor>();

    public IReadOnlyList<SettingDescriptor> Settings => _snapshot;

    public void AddSetting(SettingDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(descriptor.Key.Value))
            throw new ArgumentException("设置键不能为空。", nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.Title))
            throw new ArgumentException("设置标题不能为空。", nameof(descriptor));
        if (!_settingMap.TryAdd(descriptor.Key.Value, descriptor))
            throw new InvalidOperationException($"设置项已注册：{descriptor.Key}");

        _settings.Add(descriptor);
        RefreshSnapshot();
    }

    public bool RemoveSetting(SettingKey key)
    {
        if (string.IsNullOrWhiteSpace(key.Value) || !_settingMap.Remove(key.Value))
            return false;

        int index = _settings.FindIndex(setting => setting.Key.Equals(key.Value));
        if (index < 0)
            return false;

        _settings.RemoveAt(index);
        RefreshSnapshot();
        return true;
    }

    private void RefreshSnapshot() =>
        _snapshot = _settings.ToArray();
}

public interface IHostSettingsPageRegistry
{
    IReadOnlyList<HostSettingsPageDescriptor> Pages { get; }

    void AddPage(HostSettingsPageDescriptor descriptor);

    bool RemovePage(string id);
}

public sealed class HostSettingsPageRegistry : IHostSettingsPageRegistry
{
    private readonly List<HostSettingsPageDescriptor> _pages = [];
    private readonly Dictionary<string, HostSettingsPageDescriptor> _pageMap = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<HostSettingsPageDescriptor> _snapshot = Array.Empty<HostSettingsPageDescriptor>();

    public IReadOnlyList<HostSettingsPageDescriptor> Pages => _snapshot;

    public void AddPage(HostSettingsPageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Icon);
        if (!_pageMap.TryAdd(descriptor.Id, descriptor))
            throw new InvalidOperationException($"Host 设置页已注册：{descriptor.Id}");

        _pages.Add(descriptor);
        _snapshot = _pages.ToArray();
    }

    public bool RemovePage(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !_pageMap.Remove(id))
            return false;

        _pages.RemoveAll(page => string.Equals(page.Id, id, StringComparison.OrdinalIgnoreCase));
        _snapshot = _pages.ToArray();
        return true;
    }
}
