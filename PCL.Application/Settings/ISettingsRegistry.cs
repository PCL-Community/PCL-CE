// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Settings;

public sealed record SettingDescriptor(
    SettingKey Key,
    string Title,
    string? Description = null,
    object? DefaultValue = null);

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
