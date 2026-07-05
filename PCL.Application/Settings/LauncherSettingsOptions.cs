// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Settings;

public static class LauncherSettingsOptions
{
    public static LauncherSettings NormalizeOptionDictionaries(this LauncherSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings with
        {
            BooleanOptions = CloneOptions(settings.BooleanOptions),
            IntegerOptions = CloneOptions(settings.IntegerOptions),
            TextOptions = CloneOptions(settings.TextOptions)
        };
    }

    public static bool TryGetBooleanOption(this LauncherSettings settings, SettingKey key, out bool value)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return TryGetOption(settings.BooleanOptions, key, out value);
    }

    public static bool GetBooleanOption(this LauncherSettings settings, SettingKey key, bool fallback = false) =>
        settings.TryGetBooleanOption(key, out bool value) ? value : fallback;

    public static void SetBooleanOption(this LauncherSettings settings, SettingKey key, bool value)
    {
        ArgumentNullException.ThrowIfNull(settings);
        SetOption(settings.BooleanOptions, key, value);
    }

    public static bool TryGetIntegerOption(this LauncherSettings settings, SettingKey key, out int value)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return TryGetOption(settings.IntegerOptions, key, out value);
    }

    public static int GetIntegerOption(this LauncherSettings settings, SettingKey key, int fallback = 0) =>
        settings.TryGetIntegerOption(key, out int value) ? value : fallback;

    public static void SetIntegerOption(this LauncherSettings settings, SettingKey key, int value)
    {
        ArgumentNullException.ThrowIfNull(settings);
        SetOption(settings.IntegerOptions, key, value);
    }

    public static bool TryGetTextOption(this LauncherSettings settings, SettingKey key, out string? value)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return TryGetOption(settings.TextOptions, key, out value);
    }

    public static string GetTextOption(this LauncherSettings settings, SettingKey key, string fallback = "") =>
        settings.TryGetTextOption(key, out string? value) ? value ?? string.Empty : fallback;

    public static void SetTextOption(this LauncherSettings settings, SettingKey key, string value)
    {
        ArgumentNullException.ThrowIfNull(settings);
        SetOption(settings.TextOptions, key, value);
    }

    public static bool RemoveTextOption(this LauncherSettings settings, SettingKey key)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return RemoveOption(settings.TextOptions, key);
    }

    private static bool TryGetOption<T>(Dictionary<string, T> options, SettingKey key, out T value)
    {
        string normalizedKey = RequireKey(key);
        if (options.TryGetValue(normalizedKey, out value!))
            return true;

        foreach ((string existingKey, T existingValue) in options)
        {
            if (!string.Equals(existingKey, normalizedKey, StringComparison.OrdinalIgnoreCase))
                continue;

            value = existingValue;
            return true;
        }

        value = default!;
        return false;
    }

    private static void SetOption<T>(Dictionary<string, T> options, SettingKey key, T value)
    {
        string normalizedKey = RequireKey(key);
        string? existingKey = FindExistingKey(options, normalizedKey);
        if (existingKey is not null && !string.Equals(existingKey, normalizedKey, StringComparison.Ordinal))
            options.Remove(existingKey);

        options[normalizedKey] = value;
    }

    private static bool RemoveOption<T>(Dictionary<string, T> options, SettingKey key)
    {
        string normalizedKey = RequireKey(key);
        string? existingKey = FindExistingKey(options, normalizedKey);
        return existingKey is not null && options.Remove(existingKey);
    }

    private static string? FindExistingKey<T>(Dictionary<string, T> options, string normalizedKey)
    {
        if (options.ContainsKey(normalizedKey))
            return normalizedKey;

        foreach (string existingKey in options.Keys)
        {
            if (string.Equals(existingKey, normalizedKey, StringComparison.OrdinalIgnoreCase))
                return existingKey;
        }

        return null;
    }

    private static Dictionary<string, T> CloneOptions<T>(Dictionary<string, T>? source)
    {
        Dictionary<string, T> result = new(StringComparer.OrdinalIgnoreCase);
        if (source is null)
            return result;

        foreach ((string key, T value) in source)
        {
            if (!string.IsNullOrWhiteSpace(key))
                result[key] = value;
        }

        return result;
    }

    private static string RequireKey(SettingKey key)
    {
        if (string.IsNullOrWhiteSpace(key.Value))
            throw new ArgumentException("设置键不能为空。", nameof(key));

        return key.Value;
    }
}
