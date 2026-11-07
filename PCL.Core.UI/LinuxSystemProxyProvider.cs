using System.Diagnostics;
using System.Net.NetworkInformation;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>
///     Linux 系统代理：环境变量优先，其次 GNOME gsettings、KDE kioslaverc，最后尝试 libproxy。GNOME/KDE 设置变化会触发
///     <see cref="Changed" />。
/// </summary>
public sealed class LinuxSystemProxyProvider : ISystemProxyProvider, IDisposable
{
    private static readonly string[] _HttpProxyEnv = ["http_proxy", "HTTP_PROXY"];
    private static readonly string[] _HttpsProxyEnv = ["https_proxy", "HTTPS_PROXY"];
    private static readonly string[] _AllProxyEnv = ["all_proxy", "ALL_PROXY"];

    private readonly Lock _watchLock = new();
    private int _changeGeneration;
    private bool _disposed;
    private Process? _gnomeMonitor;
    private FileSystemWatcher? _kdeWatcher;

    public LinuxSystemProxyProvider()
    {
        try
        {
            NetworkChange.NetworkAddressChanged += _OnNetworkAddressChanged;
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Proxy", $"无法订阅网络地址变化：{ex.Message}");
        }

        _StartWatchers();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            NetworkChange.NetworkAddressChanged -= _OnNetworkAddressChanged;
        }
        catch
        {
            // 忽略退订异常
        }

        lock (_watchLock)
        {
            try
            {
                if (_gnomeMonitor is { HasExited: false })
                    _gnomeMonitor.Kill(true);
            }
            catch
            {
                // 忽略进程退出异常
            }

            _gnomeMonitor?.Dispose();
            _gnomeMonitor = null;

            _kdeWatcher?.Dispose();
            _kdeWatcher = null;
        }
    }

    public event EventHandler? Changed;

    public IReadOnlyList<SystemProxyEntry> GetSystemProxies()
    {
        var proxies = new List<SystemProxyEntry>();
        var http = _ReadEnv(_HttpProxyEnv) ?? _ReadEnv(_AllProxyEnv);
        var https = _ReadEnv(_HttpsProxyEnv) ?? _ReadEnv(_AllProxyEnv);

        if (!string.IsNullOrWhiteSpace(http)) proxies.Add(new SystemProxyEntry("http", http));
        if (!string.IsNullOrWhiteSpace(https)) proxies.Add(new SystemProxyEntry("https", https));
        if (proxies.Count > 0) return proxies;

        var gnome = _ReadGnomeProxies();
        if (gnome.Count > 0) return gnome;

        var kde = _ReadKdeProxies();
        return kde.Count > 0 ? kde : _ReadLibProxy();
    }

    private void _OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        _ScheduleChanged();
    }

    private void _ScheduleChanged()
    {
        if (_disposed) return;
        var generation = Interlocked.Increment(ref _changeGeneration);
        _ = Task.Run(async () =>
        {
            await Task.Delay(300);
            if (_disposed || generation != Volatile.Read(ref _changeGeneration))
                return;
            try
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn("Proxy", $"系统代理变化通知处理失败：{ex.Message}");
            }
        });
    }

    private void _StartWatchers()
    {
        var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? string.Empty;
        if (desktop.Contains("GNOME", StringComparison.OrdinalIgnoreCase)
            || desktop.Contains("Unity", StringComparison.OrdinalIgnoreCase))
            _StartGnomeMonitor();

        if (desktop.Contains("KDE", StringComparison.OrdinalIgnoreCase)
            || Directory.Exists(
                Path.GetDirectoryName(_GetKdeProxyPath() ?? string.Empty)))
            _StartKdeWatcher();
    }

    private void _StartGnomeMonitor()
    {
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
            startInfo.ArgumentList.Add("org.gnome.system.proxy");

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) _ScheduleChanged();
            };
            process.ErrorDataReceived += (_, _) =>
            {
                // gsettings 不可用时进程会退出，下面的 Exited 事件会清理引用。
            };
            process.Exited += (_, _) =>
            {
                lock (_watchLock)
                {
                    if (ReferenceEquals(_gnomeMonitor, process))
                        _gnomeMonitor = null;
                }

                process.Dispose();
            };
            if (!process.Start()) return;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            lock (_watchLock)
            {
                _gnomeMonitor = process;
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Proxy", $"GNOME 代理监视不可用：{ex.Message}");
        }
    }

    private void _StartKdeWatcher()
    {
        try
        {
            var path = _GetKdeProxyPath();
            if (path is null) return;
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;

            var watcher = new FileSystemWatcher(directory, Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
                    | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };
            watcher.Changed += (_, _) => _ScheduleChanged();
            watcher.Created += (_, _) => _ScheduleChanged();
            watcher.Deleted += (_, _) => _ScheduleChanged();
            watcher.Renamed += (_, _) => _ScheduleChanged();
            lock (_watchLock)
            {
                _kdeWatcher = watcher;
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Proxy", $"KDE 代理监视不可用：{ex.Message}");
        }
    }

    private static string? _GetKdeProxyPath()
    {
        try
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrWhiteSpace(configHome))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                configHome = Path.Combine(home, ".config");
            }

            return Path.Combine(configHome, "kioslaverc");
        }
        catch
        {
            return null;
        }
    }

    private static string? _ReadEnv(IEnumerable<string> names)
    {
        return (
            from name in names
            select Environment.GetEnvironmentVariable(name)
            into value
            where !string.IsNullOrWhiteSpace(value)
            select value.Trim()).FirstOrDefault();
    }

    private static IReadOnlyList<SystemProxyEntry> _ReadGnomeProxies()
    {
        var mode = _RunGsettings("get org.gnome.system.proxy mode");
        if (mode is null
            || !mode.Trim().Trim('\'').Equals("manual", StringComparison.OrdinalIgnoreCase))
            return [];

        var proxies = new List<SystemProxyEntry>();
        AddProxy("http", "org.gnome.system.proxy.http");
        AddProxy("https", "org.gnome.system.proxy.https");
        AddProxy("socks", "org.gnome.system.proxy.socks");
        return proxies;

        void AddProxy(string protocol, string schema)
        {
            var host = _RunGsettings($"get {schema} host");
            var port = _RunGsettings($"get {schema} port");
            host = host?.Trim().Trim('\'');
            if (string.IsNullOrWhiteSpace(host)) return;
            proxies.Add(
                new SystemProxyEntry(
                    protocol,
                    int.TryParse(port?.Trim(), out var p) ? $"{host}:{p}" : host));
        }
    }

    private static IReadOnlyList<SystemProxyEntry> _ReadKdeProxies()
    {
        try
        {
            var path = _GetKdeProxyPath();
            return path is not null && File.Exists(path)
                ? KdeProxyConfigParser.Parse(File.ReadAllText(path))
                : [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static IReadOnlyList<SystemProxyEntry> _ReadLibProxy()
    {
        try
        {
            using var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "proxy",
                    Arguments = "http://example.com",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            if (process is null) return [];
            if (!process.WaitForExit(1500))
            {
                process.Kill(true);
                return [];
            }

            if (process.ExitCode != 0) return [];
            var output = process.StandardOutput.ReadToEnd().Trim();
            if (output.Length == 0
                || output.StartsWith("direct://", StringComparison.OrdinalIgnoreCase))
                return [];

            var separator = output.IndexOf("://", StringComparison.Ordinal);
            if (separator < 0) return [];
            var protocol = output.StartsWith("socks", StringComparison.OrdinalIgnoreCase)
                ? "socks"
                : "http";
            var address = output[(separator + 3)..].TrimEnd('/');
            return address.Length == 0 ? [] : [new SystemProxyEntry(protocol, address)];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static string? _RunGsettings(string arguments)
    {
        try
        {
            using var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "gsettings",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            if (process is null) return null;
            if (process.WaitForExit(2000))
                return process.ExitCode == 0
                    ? process.StandardOutput.ReadToEnd()
                    : null;

            process.Kill(true);
            return null;
        }
        catch (Exception)
        {
            // 没有 gsettings（非 GNOME 桌面）时按无系统代理处理
            return null;
        }
    }
}