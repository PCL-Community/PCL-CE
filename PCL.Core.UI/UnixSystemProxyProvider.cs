using System.Diagnostics;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>
///     Unix 系统代理：优先读取环境变量，其次读取 GNOME gsettings 的 manual 代理配置。
/// </summary>
// TODO: 尚未读取 KDE Plasma（kioslaverc）与 libproxy 的代理配置，GNOME 以外的桌面可能漏掉系统代理。
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

        return _ReadGnomeProxies();
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