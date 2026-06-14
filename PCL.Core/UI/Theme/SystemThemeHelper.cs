// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Security;
using Microsoft.Win32;
using PCL.Core.Logging;

namespace PCL.Core.UI.Theme;

public static class SystemThemeHelper
{
    private const string ThemeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeKey = "AppsUseLightTheme";

    /// <summary>
    /// 检查系统是否处于深色模式。
    /// </summary>
    /// <returns>如果系统使用深色模式，则返回 true；否则返回 false（包括注册表不可访问的情况）。</returns>
    public static bool IsSystemInDarkMode()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            using var registryKey = Registry.CurrentUser.OpenSubKey(ThemeRegistryPath);
            if (registryKey is null)
            {
                LogWrapper.Warn($"注册表键 {ThemeRegistryPath} 不存在");
                return false;
            }

            var value = registryKey.GetValue(AppsUseLightThemeKey) as int?;
            return value == 0; // 0 表示深色模式（AppsUseLightTheme = false）
        }
        catch (Exception ex) when (ex is SecurityException or IOException)
        {
            LogWrapper.Warn(ex, $"无法访问注册表键 {ThemeRegistryPath}");
            return false;
        }
    }

    [Obsolete("Use ThemeService.IsDarkMode instead")]
    public static bool IsDarkMode() => ThemeService.IsDarkMode;

    /// <summary>
    /// 获取 Windows 系统强调色 (RGB)。
    /// </summary>
    public static (byte R, byte G, byte B) GetSystemAccentColor()
    {
        if (!OperatingSystem.IsWindows())
            return (0, 120, 212);

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            var value = key?.GetValue("AccentColor") as int?;
            if (value.HasValue && value.Value != 0)
            {
                // DWM AccentColor 是 ABGR 格式 (0xAABBGGRR)，按小端序读取后:
                // 低字节=RR, 次低字节=GG, 次高字节=BB, 高字节=AA
                var color = (uint)value.Value;
                byte r = (byte)color;          // 低字节 = Red
                byte g = (byte)(color >> 8);   // Green
                byte b = (byte)(color >> 16);  // Blue
                byte a = (byte)(color >> 24);  // Alpha
                LogWrapper.Debug($"System accent: A={a} R={r} G={g} B={b}");
                if (a > 0) return (r, g, b);
            }
            LogWrapper.Warn("System accent color not found or transparent, using default");
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Failed to read system accent color");
        }
        return (0, 120, 212);
    }
}
