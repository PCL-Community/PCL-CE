using System;

namespace PCL.Core.Platform;

/// <summary>
///     系统深浅色查询。具体实现由 UI 层注册到 <see cref="PlatformServices.SystemTheme" />。
/// </summary>
public interface ISystemThemeProvider
{
    /// <summary>当前是否可用（应用已初始化后才能读取系统主题）。</summary>
    bool IsAvailable { get; }

    /// <summary>系统当前是否使用深色模式。</summary>
    bool IsDarkMode { get; }

    /// <summary>系统深浅色变化通知；由实现保证在 UI 线程触发。</summary>
    event EventHandler? ThemeChanged;
}