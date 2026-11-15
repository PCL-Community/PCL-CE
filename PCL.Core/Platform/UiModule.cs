using System;

namespace PCL.Core.Platform;

/// <summary>一个平台的 Avalonia UI 服务集合。由组合根在启动时构造一次，并在退出时释放。</summary>
public sealed class UiModule : IDisposable
{
    private bool _disposed;

    public required IUiDispatcher Dispatcher { get; init; }
    public required IClipboardService Clipboard { get; init; }
    public required IFileDialogService FileDialogs { get; init; }
    public required IResourceStore ResourceStore { get; init; }
    public required ILocalizationBackend Localization { get; init; }
    public required IThemeBackend Theme { get; init; }
    public required ISystemThemeProvider SystemTheme { get; init; }
    public required ISystemNotificationService Notifications { get; init; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        (SystemTheme as IDisposable)?.Dispose();
        (Notifications as IDisposable)?.Dispose();
    }
}