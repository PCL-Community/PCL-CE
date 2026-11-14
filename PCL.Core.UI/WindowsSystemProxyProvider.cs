using Microsoft.Win32;
using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Core.UI;

/// <summary>Windows 系统代理：环境变量优先，其次 Internet Settings 注册表。</summary>
public sealed class WindowsSystemProxyProvider : ISystemProxyProvider, IDisposable
{
    private const string RegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    private readonly RegistryChangeMonitor _monitor = new(RegistryPath);

    public WindowsSystemProxyProvider()
    {
        _monitor.Changed += OnRegistryChanged;
    }

    public void Dispose()
    {
        _monitor.Changed -= OnRegistryChanged;
        _monitor.Dispose();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<SystemProxyEntry> GetSystemProxies()
    {
        var env = EnvironmentProxyReader.Read();
        if (env.Count > 0) return env;

        var enabled = Registry.GetValue(
            @"HKEY_CURRENT_USER\" + RegistryPath,
            "ProxyEnable",
            0) as int?;
        if (enabled != 1) return [];

        var server = Registry.GetValue(
            @"HKEY_CURRENT_USER\" + RegistryPath,
            "ProxyServer",
            null) as string;
        if (string.IsNullOrWhiteSpace(server)) return [];

        var result = new List<SystemProxyEntry>();
        if (server.Contains('='))
            result.AddRange(
                from segment in server.Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                let eq = segment.IndexOf('=')
                where eq > 0 && eq < segment.Length - 1
                select new SystemProxyEntry(segment[..eq], segment[(eq + 1)..]));
        else
            result.Add(new SystemProxyEntry("http", server));

        return result;
    }

    private void OnRegistryChanged(object? sender, EventArgs e)
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}