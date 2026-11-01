namespace PCL.Core.Platform;

/// <summary>
///     由 UI 层在启动时填充的平台服务。PCL.Core 通过该入口保持对 WPF/Avalonia 的无引用。
/// </summary>
public static class PlatformServices
{
    /// <summary>UI 线程调度实现。</summary>
    public static IUiDispatcher? UiDispatcher { get; set; }

    /// <summary>文件/文件夹对话框实现。</summary>
    public static IFileDialogService? FileDialogs { get; set; }

    /// <summary>应用级对话框实现。</summary>
    public static IDialogService? DialogService { get; set; }

    /// <summary>程序打包资源访问实现。</summary>
    public static IResourceStore? ResourceStore { get; set; }

    /// <summary>游戏窗口交互实现；非 Windows 平台不注册时业务层会跳过窗口相关操作。</summary>
    public static IWindowInterop? WindowInterop { get; set; }

    /// <summary>本地化 UI 后端实现。</summary>
    public static ILocalizationBackend? Localization { get; set; }

    /// <summary>主题资源后端实现。</summary>
    public static IThemeBackend? Theme { get; set; }

    /// <summary>剪贴板访问实现。</summary>
    public static IClipboardService? Clipboard { get; set; }

    /// <summary>系统代理查询实现；未注册时按无系统代理处理。</summary>
    public static ISystemProxyProvider? SystemProxy { get; set; }

    /// <summary>系统深浅色查询实现；未注册时非 Windows 平台按浅色处理。</summary>
    public static ISystemThemeProvider? SystemTheme { get; set; }

    /// <summary>系统通知实现；未注册时非 Windows 平台仅记录日志。</summary>
    public static ISystemNotificationService? Notification { get; set; }
}