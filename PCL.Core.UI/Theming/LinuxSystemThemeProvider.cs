using System.Text.RegularExpressions;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>Linux 主题：Avalonia 优先，KDE/GNOME 回退并监听设置变化。</summary>
public sealed class LinuxSystemThemeProvider : SystemThemeProvider
{
    private readonly IExternalCommandRunner _commandRunner;

    private readonly Lock _lock = new();
    private bool _dark;
    private bool _disposed;
    private IDisposable? _gnomeThemeMonitor;
    private bool _initialized;
    private FileSystemWatcher? _kdeThemeWatcher;
    private bool _refreshing;

    public LinuxSystemThemeProvider(IExternalCommandRunner commandRunner)
    {
        _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        StartWatchers();
        QueueRefresh();
    }

    public override bool IsDarkMode => !_disposed && (base.IsDarkMode || ReadDark());

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose();
        try
        {
            _gnomeThemeMonitor?.Dispose();
        }
        catch
        {
            // ignore
        }

        _gnomeThemeMonitor = null;
        _kdeThemeWatcher?.Dispose();
        _kdeThemeWatcher = null;
    }

    private bool ReadDark()
    {
        lock (_lock)
        {
            if (!_initialized && !_refreshing)
                QueueRefresh();
            return _dark;
        }
    }

    private void QueueRefresh()
    {
        if (_disposed) return;
        _refreshing = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var dark = await Task.Run(DetectDark).ConfigureAwait(false);
                bool changed;
                lock (_lock)
                {
                    changed = _initialized && dark != _dark;
                    _dark = dark;
                    _initialized = true;
                }

                if (changed) RaiseThemeChanged();
            }
            catch (Exception ex)
            {
                LogWrapper.Debug("Theme", $"系统主题回退检测失败：{ex.Message}");
            }
            finally
            {
                lock (_lock)
                {
                    _refreshing = false;
                }
            }
        });
    }

    private bool DetectDark()
    {
        return ReadKdeTheme() ?? ReadGnomeTheme() ?? false;
    }

    private static bool? ReadKdeTheme()
    {
        try
        {
            var path = Path.Combine(ConfigHome(), "kdeglobals");
            if (!File.Exists(path)) return null;
            var text = File.ReadAllText(path);
            var scheme = Regex.Match(text, @"^\s*ColorScheme\s*=\s*(.+)$", RegexOptions.Multiline);
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
            var bg = Regex.Match(
                section.Groups[1].Value,
                @"^\s*BackgroundNormal\s*=\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)",
                RegexOptions.Multiline);
            if (!bg.Success) return null;
            var r = int.Parse(bg.Groups[1].Value);
            var g = int.Parse(bg.Groups[2].Value);
            var b = int.Parse(bg.Groups[3].Value);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b < 128d;
        }
        catch
        {
            return null;
        }
    }

    private bool? ReadGnomeTheme()
    {
        try
        {
            var scheme = RunCommand(
                "gsettings",
                ["get", "org.gnome.desktop.interface", "color-scheme"]);
            if (scheme?.Contains("prefer-dark", StringComparison.OrdinalIgnoreCase) == true)
                return true;
            if (scheme?.Contains("prefer-light", StringComparison.OrdinalIgnoreCase) == true)
                return false;
            var theme = RunCommand(
                "gsettings",
                ["get", "org.gnome.desktop.interface", "gtk-theme"]);
            return theme?.Contains("dark", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }

    private string? RunCommand(string fileName, IReadOnlyList<string> arguments)
    {
        try
        {
            var result = _commandRunner
                .RunAsync(fileName, arguments, TimeSpan.FromMilliseconds(1500))
                .GetAwaiter()
                .GetResult();
            return result.Succeeded ? result.StandardOutput : null;
        }
        catch
        {
            return null;
        }
    }

    private void StartWatchers()
    {
        try
        {
            var configHome = ConfigHome();
            if (Directory.Exists(configHome))
            {
                var watcher = new FileSystemWatcher(configHome, "kdeglobals")
                {
                    NotifyFilter = NotifyFilters.FileName
                        | NotifyFilters.LastWrite
                        | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                watcher.Changed += (_, _) => Invalidate();
                watcher.Created += (_, _) => Invalidate();
                watcher.Renamed += (_, _) => Invalidate();
                _kdeThemeWatcher = watcher;
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Theme", $"KDE 主题监视不可用：{ex.Message}");
        }

        try
        {
            _gnomeThemeMonitor = _commandRunner.Watch(
                "gsettings",
                ["monitor", "org.gnome.desktop.interface"],
                _ => Invalidate());
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Theme", $"GNOME 主题监视不可用：{ex.Message}");
        }
    }

    private void Invalidate()
    {
        lock (_lock)
        {
            _initialized = false;
            if (_refreshing) return;
        }

        QueueRefresh();
    }

    private static string ConfigHome()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(configHome)) return configHome;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config");
    }
}