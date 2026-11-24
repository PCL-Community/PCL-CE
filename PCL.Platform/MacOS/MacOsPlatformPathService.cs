namespace PCL.Platform;

/// <summary>macOS 官方 Minecraft 目录。</summary>
public sealed class MacOsPlatformPathService() : PosixPlatformPathService(
    Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Application Support",
        "minecraft",
        string.Empty));