using Avalonia;
using Avalonia.Platform;
using PCL.Core.Platform;

namespace PCL.Core.Avalonia;

/// <summary>基于 Avalonia 平台设置的深浅色查询。</summary>
public sealed class AvaloniaSystemThemeProvider : ISystemThemeProvider
{
    public bool IsAvailable => Application.Current?.PlatformSettings is not null;

    public bool IsDarkMode =>
        Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant
        == PlatformThemeVariant.Dark;
}