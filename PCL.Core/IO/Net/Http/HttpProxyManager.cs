using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Win32;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;

namespace PCL.Core.IO.Net.Http;

public class HttpProxyManager : IWebProxy, IDisposable
{
    public static readonly HttpProxyManager Instance = new();

    public enum ProxyMode
    {
        NoProxy,
        SystemProxy,
        CustomProxy
    }

    private readonly object _lock = new();
    private ProxyMode _mode = ProxyMode.SystemProxy;
    private readonly WebProxy _customWebProxy = new() { BypassProxyOnLocal = true };
    private readonly WebProxy _systemWebProxy = new() { BypassProxyOnLocal = true };
    private const string ProxyRegPathFull = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private const string ProxyRegPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private readonly RegistryChangeMonitor _proxyMonitor = new(ProxyRegPath);

    private HttpProxyManager()
    {
        RefreshSystemProxy(); // 初始化系统代理
        _proxyMonitor.Changed += _OnSystemProxyChanged;
    }

    private void _OnSystemProxyChanged(object? sender, EventArgs e)
    {
        RefreshSystemProxy();
    }

    private enum ProxyProtocol
    {
        Http,
        Socks
    }

    private record ProxyItem
    {
        public ProxyProtocol Protocol;
        public required string Address;
    }

    private static ProxyItem[] _GetProxyFromString(string? proxyString)
    {
        if (proxyString.IsNullOrWhiteSpace()) return [];

        var ret = new List<ProxyItem>();

        // 形式：http=192.168.1.100:8080;socks=192.168.1.100:1080
        if (proxyString.Contains('='))
        {
            foreach (var segment in proxyString.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eqIndex = segment.IndexOf('=');
                if (eqIndex <= 0 || eqIndex >= segment.Length - 1)
                    continue;

                var protocolStr = segment[..eqIndex].Trim();
                var address = segment[(eqIndex + 1)..].Trim();

                if (string.IsNullOrWhiteSpace(address))
                    continue;

                ret.Add(new ProxyItem { Protocol = _ParseProtocol(protocolStr), Address = address });
            }

            return ret.Count > 0 ? [.. ret] : [];
        }

        // 形式：http://127.0.0.1:1145/ 或者单纯 127.0.0.1:1145
        if (Uri.TryCreate(proxyString, new UriCreationOptions(), out var proxyAddr))
        {
            var address = proxyAddr.Port > 0
                ? $"{proxyAddr.Host}:{proxyAddr.Port}"
                : proxyAddr.Host;
            ret.Add(new ProxyItem { Protocol = _ParseProtocol(proxyAddr.Scheme), Address = address });
        }
        else
        {
            ret.Add(new ProxyItem { Protocol = ProxyProtocol.Http, Address = proxyString.Trim() });
        }

        return [.. ret];
    }

    private static ProxyProtocol _ParseProtocol(string scheme)
    {
        return scheme.ToLowerInvariant() switch
        {
            "socks" or "socks4" or "socks5" => ProxyProtocol.Socks,
            _ => ProxyProtocol.Http
        };
    }

    /// <summary>刷新系统代理设置</summary>
    public void RefreshSystemProxy()
    {
        lock (_lock)
        {
            try
            {
                // read from reg
                var isSystemProxyEnabled = (int)(Registry.GetValue(ProxyRegPathFull, "ProxyEnable", 0) ?? 0);
                var systemProxyString = Registry.GetValue(ProxyRegPathFull, "ProxyServer", string.Empty) as string;

                // parse
                var proxies = _GetProxyFromString(systemProxyString);

                // filter
                if (proxies.Length == 0 || !proxies.Any(static x => x.Protocol.Equals(ProxyProtocol.Http))) isSystemProxyEnabled = 0;
                var selectedProxy = proxies.FirstOrDefault(static x => x.Protocol.Equals(ProxyProtocol.Http));

                // apply
                _systemWebProxy.Address = (isSystemProxyEnabled == 0 || selectedProxy!.Address.IsNullOrEmpty())
                    ? null
                    : new Uri($"http://{selectedProxy.Address}");

                LogWrapper.Info("Proxy",
                    $"已从操作系统更新代理设置，系统代理状态：{isSystemProxyEnabled}|{systemProxyString}");
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Proxy", "获取系统代理时出现异常");
            }
        }
    }

    public ProxyMode Mode
    {
        get { lock (_lock) return _mode; }
        set { lock (_lock) _mode = value; }
    }

    public Uri? CustomProxyAddress
    {
        get { lock (_lock) return _customWebProxy.Address; }
        set { lock (_lock) _customWebProxy.Address = value; }
    }

    public ICredentials? CustomProxyCredentials
    {
        get { lock (_lock) return _customWebProxy.Credentials; }
        set { lock (_lock) _customWebProxy.Credentials = value; }
    }

    public bool BypassOnLocal
    {
        get { lock (_lock) return field; }
        set
        {
            lock (_lock)
            {
                field = value;
                _systemWebProxy.BypassProxyOnLocal = value;
            }
        }
    } = true;

    public Uri? GetProxy(Uri destination)
    {
        lock (_lock)
        {
            return _mode switch
            {
                ProxyMode.NoProxy => null, // 返回 null 表明没有代理
                ProxyMode.SystemProxy => _systemWebProxy.GetProxy(destination),
                ProxyMode.CustomProxy => _customWebProxy.GetProxy(destination),
                _ => null
            };
        }
    }

    public bool IsBypassed(Uri host)
    {
        lock (_lock)
        {
            return _mode switch
            {
                ProxyMode.NoProxy => true,
                ProxyMode.SystemProxy => _systemWebProxy.IsBypassed(host),
                ProxyMode.CustomProxy => _customWebProxy.IsBypassed(host),
                _ => true
            };
        }
    }

    public ICredentials? Credentials
    {
        get
        {
            lock (_lock)
            {
                // 仅 CustomProxy 模式返回凭据
                return _mode == ProxyMode.CustomProxy
                    ? _customWebProxy.Credentials
                    : null;
            }
        }
        set
        {
            lock (_lock)
            {
                _customWebProxy.Credentials = value;
            }
        }
    }

    public void Dispose()
    {
        _proxyMonitor.Dispose();
        GC.SuppressFinalize(this);
    }
}
