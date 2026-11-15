namespace PCL.Core.Platform;

/// <summary>一个平台的 Avalonia UI 服务集合。由组合根在启动时构造一次。</summary>
public sealed class UiModule
{
    public required IUiDispatcher Dispatcher { get; init; }
    public required IClipboardService Clipboard { get; init; }
    public required IFileDialogService FileDialogs { get; init; }
    public required IResourceStore ResourceStore { get; init; }
    public required ILocalizationBackend Localization { get; init; }
    public required IThemeBackend Theme { get; init; }
    public required ISystemThemeProvider SystemTheme { get; init; }
    public required ISystemNotificationService Notifications { get; init; }
}