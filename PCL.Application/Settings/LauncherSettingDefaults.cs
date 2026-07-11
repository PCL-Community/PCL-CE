// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Settings;

public static class LauncherSettingDefaults
{
    private static readonly Dictionary<string, bool> BooleanDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SystemDisableHardwareAcceleration"] = false,
        ["SystemNetEnableDoH"] = true,
        ["SystemDebugMode"] = false,
        ["SystemDebugDelay"] = false,
        ["SystemDebugSkipCopy"] = false,
        ["ToolDownloadAutoSelectVersion"] = true,
        ["ToolFixAuthlib"] = true,
        ["ToolDownloadIgnoreQuilt"] = false,
        ["ToolDownloadAutoInstallDependencies"] = true,
        ["ToolHelpChinese"] = true,
        ["ToolUpdateRelease"] = false,
        ["ToolUpdateSnapshot"] = false,
        ["UiLauncherLogo"] = true,
        ["UiLockWindowSize"] = false,
        ["UiShowLaunchingHint"] = true,
        ["UiHintAlignRight"] = false,
        ["UiLogoLeft"] = false,
        ["UiBackgroundColorful"] = true,
        ["UiAutoPauseVideo"] = true,
        ["UiBlur"] = false,
        ["UiMusicStop"] = false,
        ["UiMusicStart"] = false,
        ["UiMusicAuto"] = true,
        ["UiMusicRandom"] = true,
        ["UiMusicSMTC"] = true,
        ["LaunchAdvanceRunWait"] = true,
        ["LaunchAdvanceDisableJLW"] = true,
        ["LaunchAdvanceDisableRW"] = false,
        ["LaunchAdvanceGraphicCard"] = true,
        ["LaunchAdvanceNoJavaw"] = false,
        ["LaunchAdvanceDisableLwjglUnsafeAgent"] = false,
        ["LaunchAutoRepairGame"] = true
    };

    private static readonly Dictionary<string, int> IntegerDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SystemMaxLog"] = 13,
        ["UiAniFPS"] = 59,
        ["SystemHttpProxyType"] = 1,
        ["SystemDebugAnim"] = 9,
        ["ToolDownloadThread"] = 63,
        ["ToolDownloadSpeed"] = 42,
        ["ToolDownloadSource"] = 1,
        ["ToolDownloadVersion"] = 1,
        ["ToolDownloadTranslateV2"] = 1,
        ["ToolDownloadMod"] = 1,
        ["ToolModLocalNameStyle"] = 0,
        ["SystemUpdateMode"] = 1,
        ["SystemUpdateChannel"] = 0,
        ["SystemSystemActivity"] = 0,
        ["UiDarkMode"] = 2,
        ["UiLauncherTransparent"] = 600,
        ["UiBackgroundOpacity"] = 1000,
        ["UiBackgroundBlur"] = 0,
        ["UiBackgroundSuit"] = 0,
        ["UiBlurValue"] = 16,
        ["UiBlurSamplingRate"] = 70,
        ["UiBlurType"] = 0,
        ["UiCustomType"] = 0,
        ["UiCustomPreset"] = 14,
        ["UiMusicVolume"] = 500,
        ["UiLogoType"] = 1,
        ["LaunchRamType"] = 0,
        ["LaunchRamCustom"] = 15,
        ["LaunchPreferredIpStack"] = 1,
        ["LaunchAdvanceRenderer"] = 0,
        ["LaunchArgumentIndieV2"] = 4,
        ["LaunchArgumentVisible"] = 5,
        ["LaunchArgumentPriority"] = 1,
        ["LaunchArgumentWindowWidth"] = 854,
        ["LaunchArgumentWindowHeight"] = 480,
        ["LaunchArgumentWindowType"] = 1,
        ["LoginMsAuthType"] = 1
    };

    private static readonly Dictionary<string, string> TextDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SystemHttpProxy"] = string.Empty,
        ["SystemHttpProxyCustomUsername"] = string.Empty,
        ["SystemHttpProxyCustomPassword"] = string.Empty,
        ["UiLogoText"] = string.Empty,
        ["UiCustomNet"] = string.Empty,
        ["UiLanguage"] = "auto",
        ["UiFormatCulture"] = "auto",
        ["LaunchAdvanceJvm"] = "-XX:+UseG1GC -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow -Djdk.lang.Process.allowAmbiguousCommands=true -Dfml.ignoreInvalidMinecraftCertificates=True -Dfml.ignorePatchDiscrepancies=True -Dlog4j2.formatMsgNoLookups=true",
        ["LaunchAdvanceGame"] = string.Empty,
        ["LaunchAdvanceRun"] = string.Empty,
        ["LaunchArgumentTitle"] = string.Empty,
        ["LaunchArgumentInfo"] = "PCLN"
    };

    public static bool GetBoolean(string key, bool fallback = false) =>
        BooleanDefaults.TryGetValue(key, out bool value) ? value : fallback;

    public static int GetInteger(string key, int fallback = 0) =>
        IntegerDefaults.TryGetValue(key, out int value) ? value : fallback;

    public static string GetText(string key, string fallback = "") =>
        TextDefaults.TryGetValue(key, out string? value) ? value : fallback;
}
