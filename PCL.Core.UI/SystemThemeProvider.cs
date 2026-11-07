using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>Avalonia 平台主题事件与缓存。</summary>
public class SystemThemeProvider : ISystemThemeProvider, IDisposable
{
    private bool? _lastDark;
    private bool _subscribed;
    private bool _subscribing;

    public SystemThemeProvider()
    {
        TrySubscribe();
    }

    public virtual void Dispose()
    {
        if (_subscribed && Application.Current?.PlatformSettings is { } settings)
            settings.ColorValuesChanged -= OnColorValuesChanged;
        _subscribed = false;
    }

    public virtual bool IsAvailable
    {
        get
        {
            TrySubscribe();
            return Application.Current?.PlatformSettings is not null;
        }
    }

    public virtual bool IsDarkMode
    {
        get
        {
            TrySubscribe();
            return Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant
                == PlatformThemeVariant.Dark;
        }
    }

    public event EventHandler? ThemeChanged;

    protected void TrySubscribe()
    {
        if (_subscribed || _subscribing) return;
        var settings = Application.Current?.PlatformSettings;
        if (settings is null) return;

        _subscribing = true;
        try
        {
            settings.ColorValuesChanged += OnColorValuesChanged;
            _lastDark = settings.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;
            _subscribed = true;
        }
        finally
        {
            _subscribing = false;
        }
    }

    protected virtual void OnColorValuesChanged(object? sender, PlatformColorValues e)
    {
        var isDark = e.ThemeVariant == PlatformThemeVariant.Dark;
        if (_lastDark == isDark) return;
        _lastDark = isDark;
        RaiseThemeChanged();
    }

    protected void RaiseThemeChanged()
    {
        Dispatcher.UIThread.Post(() => ThemeChanged?.Invoke(this, EventArgs.Empty));
    }
}