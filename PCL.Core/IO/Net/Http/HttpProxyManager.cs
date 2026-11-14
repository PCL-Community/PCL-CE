using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PCL.Core.Utils.Exts;

namespace PCL.Core.IO.Net.Http;

public class HttpProxyManager : IWebProxy, IDisposable
{
    public enum ProxyMode
    {
        NoProxy,
        SystemProxy,
        CustomProxy
    }

    public static readonly HttpProxyManager Instance = new();

    private readonly WebProxy _customWebProxy = new()
    {
        BypassProxyOnLocal = true
    };

    private readonly Lock _lock = new();

    private readonly WebProxy _systemWebProxy = new()
    {
        BypassProxyOnLocal = true
    };

    private ProxyMode _mode = ProxyMode.SystemProxy;

    private ISystemProxyProvider? _subscribedProxyProvider;

    private HttpProxyManager()
    {
        _EnsurePlatformProxySubscription();
        _ = Task.Run(RefreshSystemProxy);
    }

    public ProxyMode Mode
    {
        get
        {
            lock (_lock)
            {
                return _mode;
            }
        }
        set
        {
            lock (_lock)
            {
                _mode = value;
            }
        }
    }

    public Uri? CustomProxyAddress
    {
        get
        {
            lock (_lock)
            {
                return _customWebProxy.Address;
            }
        }
        set
        {
            lock (_lock)
            {
                _customWebProxy.Address = value;
            }
        }
    }

    public ICredentials? CustomProxyCredentials
    {
        get
        {
            lock (_lock)
            {
                return _customWebProxy.Credentials;
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

    public bool BypassOnLocal
    {
        get
        {
            lock (_lock)
            {
                return field;
            }
        }
        set
        {
            lock (_lock)
            {
                field = value;
                _systemWebProxy.BypassProxyOnLocal = value;
            }
        }
    } = true;

    public void Dispose()
    {
        if (_subscribedProxyProvider is not null)
            _subscribedProxyProvider.Changed -= _OnSystemProxyChanged;
        _subscribedProxyProvider = null;
        GC.SuppressFinalize(this);
    }

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

    private void _EnsurePlatformProxySubscription()
    {
        var provider = PlatformServices.SystemProxy;
        if (ReferenceEquals(_subscribedProxyProvider, provider)) return;

        if (_subscribedProxyProvider is not null)
            _subscribedProxyProvider.Changed -= _OnSystemProxyChanged;

        _subscribedProxyProvider = provider;
        if (provider is not null)
            provider.Changed += _OnSystemProxyChanged;
    }

    private void _OnSystemProxyChanged(object? sender, EventArgs e)
    {
        _ = Task.Run(RefreshSystemProxy);
    }

    private static ProxyItem[] _GetProxyFromString(string? proxyString)
    {
        if (proxyString.IsNullOrWhiteSpace()) return [];

        // 含 '=' 的形式：http=192.168.1.100:8080;socks=192.168.1.100:1080;ftp=127.0.0.1:124
        // 也可以是 http=http://127.0.0.1:10808
        if (proxyString.Contains('='))
        {
            var items = new List<ProxyItem>();
            foreach (var segment in proxyString.Split(
                         ';',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (_ParseKeyValueSegment(segment) is { } item)
                    items.Add(item);

            return [.. items];
        }

        // 形式：http://127.0.0.1:1145/ 或者单纯 127.0.0.1:1145
        var proxy = _ParseAddress(proxyString.Trim());
        return proxy is null ? [] : [proxy];
    }

    /// <summary>解析 <c>protocol=address</c> 形式的段；格式非法时返回 null 以便忽略该段</summary>
    private static ProxyItem? _ParseKeyValueSegment(string segment)
    {
        var eqIndex = segment.IndexOf('=');
        // 缺少 '='、协议名或地址的段视为非法，直接忽略
        if (eqIndex <= 0 || eqIndex >= segment.Length - 1) return null;

        return _ParseAddress(
            segment[(eqIndex + 1)..].Trim(),
            _ParseProtocol(segment[..eqIndex].Trim()));
    }

    /// <summary>解析单个代理地址：支持带 scheme 的地址（如 http://127.0.0.1:10808）与纯 host:port 地址（如 127.0.0.1:1145）</summary>
    private static ProxyItem? _ParseAddress(string address, ProxyProtocol? protocol = null)
    {
        if (address.IsNullOrWhiteSpace()) return null;

        // 能解析出主机名的地址，规范化为 host:port
        if (Uri.TryCreate(address, new UriCreationOptions(), out var uri)
            && !uri.Host.IsNullOrEmpty())
        {
            var hostPort = uri.Port > 0 ? $"{uri.Host}:{uri.Port}" : uri.Host;
            return new ProxyItem(protocol ?? _ParseProtocol(uri.Scheme), hostPort);
        }

        // 纯 host:port 地址（无法作为 URI 解析），按指定协议（默认 Http）原样使用
        return new ProxyItem(protocol ?? ProxyProtocol.Http, address);
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
        _EnsurePlatformProxySubscription();
        lock (_lock)
        {
            try
            {
                var platformProxies = PlatformServices.SystemProxy?.GetSystemProxies() ?? [];
                ProxyItem? platformProxy = null;
                foreach (var entry in platformProxies)
                {
                    var parsed = _ParseAddress(entry.Address, _ParseProtocol(entry.Protocol));
                    if (parsed is not { Protocol: ProxyProtocol.Http }) continue;
                    platformProxy = parsed;
                    break;
                }

                _systemWebProxy.Address = platformProxy is null
                    || platformProxy.Address.IsNullOrEmpty()
                        ? null
                        : new Uri($"http://{platformProxy.Address}");
                LogWrapper.Info(
                    "Proxy",
                    $"已从操作系统更新代理设置，系统代理来源：{string.Join(";", platformProxies.Select(static x => $"{x.Protocol}={x.Address}"))}");
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Proxy", "获取系统代理时出现异常");
            }
        }
    }

    private enum ProxyProtocol
    {
        Http,
        Socks
    }

    private sealed record ProxyItem(ProxyProtocol Protocol, string Address);
}