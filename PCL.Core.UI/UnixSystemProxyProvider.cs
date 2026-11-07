using System.Diagnostics;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>
///     Unix 系统代理：环境变量优先，其次 GNOME gsettings、KDE kioslaverc，最后尝试 libproxy。
/// </summary>
public sealed class UnixSystemProxyProvider : ISystemProxyProvider
{
    private static readonly string[] _HttpProxyEnv = ["http_proxy", "HTTP_PROXY"];
    private static readonly string[] _HttpsProxyEnv = ["https_proxy", "HTTPS_PROXY"];
    private static readonly string[] _AllProxyEnv = ["all_proxy", "ALL_PROXY"];

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
        if (!OperatingSystem.IsLinux()) return [];
        try
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrWhiteSpace(configHome))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                configHome = Path.Combine(home, ".config");
            }

            var path = Path.Combine(configHome, "kioslaverc");
            return File.Exists(path)
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
        if (!OperatingSystem.IsLinux()) return [];
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