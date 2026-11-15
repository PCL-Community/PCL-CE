namespace PCL.Core.Platform;

/// <summary>旧版平台服务访问入口。仅作为迁移期的只读兼容门面； 新代码请使用 <see cref="ApplicationRuntime" />。</summary>
public static class PlatformServices
{
    private static ApplicationServices? Current =>
        ApplicationRuntime.IsInitialized ? ApplicationRuntime.Current : null;

    /// <summary>UI 线程调度实现。</summary>
    public static IUiDispatcher? UiDispatcher => Current?.Ui.Dispatcher;

    /// <summary>文件/文件夹对话框实现。</summary>
    public static IFileDialogService? FileDialogs => Current?.Ui.FileDialogs;

    /// <summary>致命错误对话框实现。</summary>
    public static IFatalDialogService? FatalDialog => Current?.Platform.FatalDialog;

    /// <summary>程序打包资源访问实现。</summary>
    public static IResourceStore? ResourceStore => Current?.Ui.ResourceStore;

    /// <summary>游戏窗口交互实现。</summary>
    public static IWindowInterop? WindowInterop => Current?.Platform.WindowInterop;

    /// <summary>本地化 UI 后端实现。</summary>
    public static ILocalizationBackend? Localization => Current?.Ui.Localization;

    /// <summary>主题资源后端实现。</summary>
    public static IThemeBackend? Theme => Current?.Ui.Theme;

    /// <summary>剪贴板访问实现。</summary>
    public static IClipboardService? Clipboard => Current?.Ui.Clipboard;

    /// <summary>系统代理查询实现。</summary>
    public static ISystemProxyProvider? SystemProxy => Current?.Platform.SystemProxy;

    /// <summary>系统深浅色查询实现。</summary>
    public static ISystemThemeProvider? SystemTheme => Current?.Ui.SystemTheme;

    /// <summary>系统通知实现。</summary>
    public static ISystemNotificationService? Notifications => Current?.Ui.Notifications;
}