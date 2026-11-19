using System;

namespace PCL.Core.Platform;

/// <summary>Avalonia UI 服务门面。服务实例由组合根注册到 <see cref="ApplicationRuntime" />。</summary>
public sealed class UiModule : IDisposable
{
    public IUiDispatcher Dispatcher => _Get<IUiDispatcher>();
    public IClipboardService Clipboard => _Get<IClipboardService>();
    public IFileDialogService FileDialogs => _Get<IFileDialogService>();
    public IResourceStore ResourceStore => _Get<IResourceStore>();
    public ILocalizationBackend Localization => _Get<ILocalizationBackend>();
    public IThemeBackend Theme => _Get<IThemeBackend>();
    public ISystemThemeProvider SystemTheme => _Get<ISystemThemeProvider>();
    public ISystemNotificationService Notifications => _Get<ISystemNotificationService>();

    public void Dispose()
    {
        // 服务的释放由 DI 容器负责。
    }

    private static T _Get<T>() where T : class
    {
        return ApplicationRuntime.GetRequiredService<T>();
    }
}