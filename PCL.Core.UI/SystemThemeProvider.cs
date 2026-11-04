using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>基于 Avalonia 平台设置的深浅色查询与变化通知。</summary>
public sealed class SystemThemeProvider : ISystemThemeProvider, IDisposable
{
    private bool? _lastDark;
    private bool _subscribed;
    private bool _subscribing;

    public SystemThemeProvider()
    {
        _TrySubscribe();
    }

    public void Dispose()
    {
        if (!_subscribed) return;
        if (Application.Current?.PlatformSettings is { } settings)
            settings.ColorValuesChanged -= _OnColorValuesChanged;
        _subscribed = false;
    }

    public bool IsAvailable
    {
        get
        {
            _TrySubscribe();
            return Application.Current?.PlatformSettings is not null;
        }
    }

    public bool IsDarkMode
    {
        get
        {
            _TrySubscribe();
            return Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant
                == PlatformThemeVariant.Dark;
        }
    }

    public event EventHandler? ThemeChanged;

    private void _TrySubscribe()
    {
        if (_subscribed || _subscribing) return;
        var settings = Application.Current?.PlatformSettings;
        if (settings is null) return;

        _subscribing = true;
        try
        {
            settings.ColorValuesChanged += _OnColorValuesChanged;
            _lastDark = settings.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;
            _subscribed = true;
        }
        finally
        {
            _subscribing = false;
        }
    }

    private void _OnColorValuesChanged(object? sender, PlatformColorValues e)
    {
        var isDark = e.ThemeVariant == PlatformThemeVariant.Dark;
        if (_lastDark == isDark) return;
        _lastDark = isDark;
        Dispatcher.UIThread.Post(() => ThemeChanged?.Invoke(this, EventArgs.Empty));
    }
}