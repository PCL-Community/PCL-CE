// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Settings;

public static class LauncherSettingKeys
{
    public static readonly SettingKey LaunchAdvanceJvm = new("LaunchAdvanceJvm");

    public static readonly SettingKey LaunchAdvanceGame = new("LaunchAdvanceGame");

    public static readonly SettingKey LaunchArgumentWindowHeight = new("LaunchArgumentWindowHeight");

    public static readonly SettingKey LaunchArgumentWindowType = new("LaunchArgumentWindowType");

    public static readonly SettingKey LaunchArgumentWindowWidth = new("LaunchArgumentWindowWidth");

    public static readonly SettingKey LaunchPreferredIpStack = new("LaunchPreferredIpStack");

    public static readonly SettingKey LaunchRamCustom = new("LaunchRamCustom");

    public static readonly SettingKey LaunchRamType = new("LaunchRamType");

    public static readonly SettingKey LaunchSelectedJava = new("LaunchSelectedJava");

    public static readonly SettingKey LaunchSelectedInstanceDirectory = new("LaunchSelectedInstanceDirectory");

    public static readonly SettingKey LaunchMinecraftFolders = new("LaunchMinecraftFolders");

    public static readonly SettingKey LaunchSelectedMinecraftRoot = new("LaunchSelectedMinecraftRoot");

    public static readonly SettingKey JavaCustomRoots = new("JavaCustomRoots");

    public static readonly SettingKey HintDownloadThread = new("HintDownloadThread");

    public static readonly SettingKey ToolDownloadThread = new("ToolDownloadThread");

    public static readonly SettingKey UiCustomLogoPath = new("UiCustomLogoPath");

    public static SettingKey JavaDisabled(string javaExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(javaExecutablePath))
            throw new ArgumentException("Java 可执行文件路径不能为空。", nameof(javaExecutablePath));

        return new SettingKey("JavaDisabled|" + javaExecutablePath);
    }
}
