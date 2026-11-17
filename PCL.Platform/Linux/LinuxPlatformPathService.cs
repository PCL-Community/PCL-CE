namespace PCL.Platform;

/// <summary>Linux 官方 Minecraft 目录。</summary>
public sealed class LinuxPlatformPathService() : PosixPlatformPathService(
    Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".minecraft",
        string.Empty));