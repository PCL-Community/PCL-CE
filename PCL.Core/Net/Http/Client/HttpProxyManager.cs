using System;
using System.Net;
using Microsoft.Win32;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;

namespace PCL.Core.Net.Http.Client;

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
    private readonly WebProxy _customWebProxy = new() {BypassProxyOnLocal = true};
    private readonly WebProxy _systemWebProxy = new() {BypassProxyOnLocal = true};
    private const string ProxyRegPathFull = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private const string ProxyRegPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private readonly RegistryChangeMonitor _proxyMonitor = new(ProxyRegPath);

    private HttpProxyManager()
    {
        RefreshSystemProxy(); // 初始化系统代理
        _proxyMonitor.Changed += _onSystemProxyChanged;
    }

    private void _onSystemProxyChanged(object? sender, EventArgs e)
    {
        RefreshSystemProxy();
    }

    /// <summary>刷新系统代理设置</summary>
    public void RefreshSystemProxy()
    {
        lock (_lock)
        {
            try
            {
                var isSystemProxyEnabled = (int)(Registry.GetValue(ProxyRegPathFull, "ProxyEnable", 0) ?? 0);
                var systemProxyAddress = Registry.GetValue(ProxyRegPathFull, "ProxyServer", string.Empty) as string;
                if (systemProxyAddress is not null && !systemProxyAddress.StartsWith("http")) systemProxyAddress = $"http://{systemProxyAddress}/";
                _systemWebProxy.Address = (string.IsNullOrEmpty(systemProxyAddress) || isSystemProxyEnabled == 0)
                    ? null
                    : new Uri(systemProxyAddress);
                LogWrapper.Info("Proxy",
                    $"已从操作系统更新代理设置，系统代理状态：{isSystemProxyEnabled}|{systemProxyAddress}");
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