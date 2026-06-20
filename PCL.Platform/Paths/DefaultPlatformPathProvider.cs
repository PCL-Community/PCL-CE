// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Platform.Abstractions.Paths;

namespace PCL.Platform.Paths;

public sealed class DefaultPlatformPathProvider : IPlatformPathProvider
{
    public string ApplicationDataDirectory => GetApplicationDataDirectory();
    public string CacheDirectory => GetCacheDirectory();
    public string TemporaryDirectory => Path.GetFullPath(Path.GetTempPath());

    private static string GetApplicationDataDirectory()
    {
        if (OperatingSystem.IsWindows())
            return GetSpecialFolder(Environment.SpecialFolder.ApplicationData);

        string homeDirectory = GetHomeDirectory();
        if (OperatingSystem.IsMacOS())
            return Path.Combine(homeDirectory, "Library", "Application Support");

        string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return !string.IsNullOrWhiteSpace(xdgDataHome)
            ? Path.GetFullPath(xdgDataHome)
            : Path.Combine(homeDirectory, ".local", "share");
    }

    private static string GetCacheDirectory()
    {
        if (OperatingSystem.IsWindows())
            return GetSpecialFolder(Environment.SpecialFolder.LocalApplicationData);

        string homeDirectory = GetHomeDirectory();
        if (OperatingSystem.IsMacOS())
            return Path.Combine(homeDirectory, "Library", "Caches");

        string? xdgCacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        return !string.IsNullOrWhiteSpace(xdgCacheHome)
            ? Path.GetFullPath(xdgCacheHome)
            : Path.Combine(homeDirectory, ".cache");
    }

    private static string GetSpecialFolder(Environment.SpecialFolder folder)
    {
        string path = Environment.GetFolderPath(folder);
        return !string.IsNullOrWhiteSpace(path)
            ? Path.GetFullPath(path)
            : GetHomeDirectory();
    }

    private static string GetHomeDirectory()
    {
        string? home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(home))
            return Path.GetFullPath(home);

        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
            return Path.GetFullPath(profile);

        return Path.GetFullPath(Path.GetTempPath());
    }
}
