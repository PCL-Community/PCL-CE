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

    public IReadOnlyList<SettingDescriptor> Settings => _settings.ToArray();

    public void AddSetting(SettingDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(descriptor.Key.Value))
            throw new ArgumentException("设置键不能为空。", nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.Title))
            throw new ArgumentException("设置标题不能为空。", nameof(descriptor));
        if (_settings.Any(setting => setting.Key.Equals(descriptor.Key.Value)))
            throw new InvalidOperationException($"设置项已注册：{descriptor.Key}");

        _settings.Add(descriptor);
    }

    public bool RemoveSetting(SettingKey key)
    {
        int index = _settings.FindIndex(setting => setting.Key.Equals(key.Value));
        if (index < 0)
            return false;

        _settings.RemoveAt(index);
        return true;
    }
}
