using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>
///     Avalonia 平台设置的深浅色查询与变化通知；Linux 在 Avalonia 未报告深色时
///     回退读取 KDE kdeglobals / GNOME gsettings，并监听设置变化。
/// </summary>
public sealed class SystemThemeProvider : ISystemThemeProvider, IDisposable
{
    private readonly Lock _linuxLock = new();
    private Process? _gnomeThemeMonitor;
    private FileSystemWatcher? _kdeThemeWatcher;
    private bool? _lastDark;
    private bool _linuxDark;
    private bool _linuxThemeInitialized;
    private bool _linuxThemeRefreshing;
    private bool _subscribed;
    private bool _subscribing;

    public SystemThemeProvider()
    {
        _TrySubscribe();
        _StartLinuxWatchers();
        if (OperatingSystem.IsLinux())
            _QueueLinuxThemeRefresh();
    }

    public void Dispose()
    {
        if (_subscribed && Application.Current?.PlatformSettings is { } settings)
            settings.ColorValuesChanged -= _OnColorValuesChanged;
        _subscribed = false;

        try
        {
            if (_gnomeThemeMonitor is { HasExited: false })
                _gnomeThemeMonitor.Kill(true);
        }
        catch
        {
            // 忽略退出异常
        }

        _gnomeThemeMonitor?.Dispose();
        _gnomeThemeMonitor = null;
        _kdeThemeWatcher?.Dispose();
        _kdeThemeWatcher = null;
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
            var variant = Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant;
            return variant == PlatformThemeVariant.Dark
                || (OperatingSystem.IsLinux()
                    && _ReadLinuxDark());
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

    private bool _ReadLinuxDark()
    {
        lock (_linuxLock)
        {
            if (!_linuxThemeInitialized && !_linuxThemeRefreshing)
                _QueueLinuxThemeRefresh();
            return _linuxDark;
        }
    }

    private void _QueueLinuxThemeRefresh()
    {
        _linuxThemeRefreshing = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var dark = await Task.Run(_DetectLinuxDark).ConfigureAwait(false);
                bool changed;
                lock (_linuxLock)
                {
                    changed = _linuxThemeInitialized && dark != _linuxDark;
                    _linuxDark = dark;
                    _linuxThemeInitialized = true;
                }

                if (changed)
                    Dispatcher.UIThread.Post(() => ThemeChanged?.Invoke(this, EventArgs.Empty));
            }
            catch (Exception ex)
            {
                LogWrapper.Debug("Theme", $"系统主题回退检测失败：{ex.Message}");
            }
            finally
            {
                lock (_linuxLock)
                {
                    _linuxThemeRefreshing = false;
                }
            }
        });
    }

    private static bool _DetectLinuxDark()
    {
        var kde = _ReadKdeTheme();
        if (kde is not null) return kde.Value;

        var gnome = _ReadGnomeTheme();
        return gnome ?? false;
    }

    private static bool? _ReadKdeTheme()
    {
        try
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrWhiteSpace(configHome))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                configHome = Path.Combine(home, ".config");
            }

            var path = Path.Combine(configHome, "kdeglobals");
            if (!File.Exists(path)) return null;

            var text = File.ReadAllText(path);
            var scheme = Regex.Match(
                text,
                @"^\s*ColorScheme\s*=\s*(.+)$",
                RegexOptions.Multiline);
            if (scheme.Success)
            {
                var name = scheme.Groups[1].Value.Trim();
                if (name.Contains("dark", StringComparison.OrdinalIgnoreCase)) return true;
                if (name.Contains("light", StringComparison.OrdinalIgnoreCase)) return false;
            }

            var section = Regex.Match(
                text,
                @"\[Colors:Window\]([\s\S]*?)(?:\n\[|\z)",
                RegexOptions.Multiline);
            if (!section.Success) return null;
            var background = Regex.Match(
                section.Groups[1].Value,
                @"^\s*BackgroundNormal\s*=\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)",
                RegexOptions.Multiline);
            if (!background.Success) return null;
            var r = int.Parse(background.Groups[1].Value);
            var g = int.Parse(background.Groups[2].Value);
            var b = int.Parse(background.Groups[3].Value);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b < 128d;
        }
        catch
        {
            return null;
        }
    }

    private static bool? _ReadGnomeTheme()
    {
        try
        {
            var scheme = _RunCommand(
                "gsettings",
                ["get", "org.gnome.desktop.interface", "color-scheme"]);
            if (scheme?.Contains("prefer-dark", StringComparison.OrdinalIgnoreCase) == true)
                return true;
            if (scheme?.Contains("prefer-light", StringComparison.OrdinalIgnoreCase) == true)
                return false;

            var theme = _RunCommand(
                "gsettings",
                ["get", "org.gnome.desktop.interface", "gtk-theme"]);
            return theme?.Contains("dark", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }

    private static string? _RunCommand(string fileName, IReadOnlyList<string> arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process is null)
                return null;

            if (process.WaitForExit(1500))
                return process.ExitCode == 0
                    ? process.StandardOutput.ReadToEnd()
                    : null;

            process.Kill(true);
            return null;
        }
        catch
        {
            return null;
        }
    }

    private void _StartLinuxWatchers()
    {
        if (!OperatingSystem.IsLinux()) return;

        try
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrWhiteSpace(configHome))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                configHome = Path.Combine(home, ".config");
            }

            if (Directory.Exists(configHome))
            {
                var watcher = new FileSystemWatcher(configHome, "kdeglobals")
                {
                    NotifyFilter = NotifyFilters.FileName
                        | NotifyFilters.LastWrite
                        | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                watcher.Changed += (_, _) => _InvalidateLinuxTheme();
                watcher.Created += (_, _) => _InvalidateLinuxTheme();
                watcher.Renamed += (_, _) => _InvalidateLinuxTheme();
                _kdeThemeWatcher = watcher;
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Theme", $"KDE 主题监视不可用：{ex.Message}");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "gsettings",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("monitor");
            startInfo.ArgumentList.Add("org.gnome.desktop.interface");
            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) _InvalidateLinuxTheme();
            };
            process.Exited += (_, _) =>
            {
                process.Dispose();
                if (ReferenceEquals(_gnomeThemeMonitor, process))
                    _gnomeThemeMonitor = null;
            };
            if (process.Start())
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _gnomeThemeMonitor = process;
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Theme", $"GNOME 主题监视不可用：{ex.Message}");
        }
    }

    private void _InvalidateLinuxTheme()
    {
        lock (_linuxLock)
        {
            _linuxThemeInitialized = false;
            if (_linuxThemeRefreshing)
                return;
        }

        _QueueLinuxThemeRefresh();
    }
}