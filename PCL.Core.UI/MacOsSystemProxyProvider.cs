using System.Diagnostics;
using System.Net.NetworkInformation;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>macOS 系统代理：环境变量优先，其次 scutil --proxy。</summary>
public sealed class MacOsSystemProxyProvider : ISystemProxyProvider, IDisposable
{
    private bool _disposed;
    private int _generation;

    public MacOsSystemProxyProvider()
    {
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<SystemProxyEntry> GetSystemProxies()
    {
        var env = EnvironmentProxyReader.Read();
        return env.Count > 0 ? env : ReadScutil();
    }

    private void OnNetworkChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        var generation = Interlocked.Increment(ref _generation);
        _ = Task.Run(async () =>
        {
            await Task.Delay(300);
            if (_disposed || generation != Volatile.Read(ref _generation)) return;
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    private static IReadOnlyList<SystemProxyEntry> ReadScutil()
    {
        try
        {
            using var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "scutil",
                    Arguments = "--proxy",
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

            return process.ExitCode == 0
                ? MacOsProxyConfigParser.Parse(process.StandardOutput.ReadToEnd())
                : [];
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Proxy", $"macOS 系统代理读取失败：{ex.Message}");
            return [];
        }
    }
}