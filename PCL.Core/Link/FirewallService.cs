using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace PCL.Core.Link;

/// <summary>
///     Windows 防火墙规则管理。支持 netsh advfirewall 方式。
///     添加规则需要管理员权限。
/// </summary>
public sealed class FirewallService : IDisposable
{
    private readonly List<string> _rules = [];
    private bool _disposed;

    /// <summary>
    ///     添加入站端口放行规则。
    /// </summary>
    /// <param name="port">端口号</param>
    /// <param name="protocol">TCP 或 UDP</param>
    /// <param name="name">规则名称（用于后续删除）</param>
    /// <returns>是否成功</returns>
    public bool AddInboundRule(int port, string protocol, string name)
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            var args =
                $"advfirewall firewall add rule name=\"{name}\" dir=in action=allow protocol={protocol.ToUpper()} localport={port}";
            var result = RunNetsh(args);
            if (result.ExitCode == 0)
            {
                _rules.Add(name);
                return true;
            }
        }
        catch
        {
            // 无管理员权限时失败
        }

        return false;
    }

    /// <summary>
    ///     删除指定名称的防火墙规则。
    /// </summary>
    public void DeleteRule(string name)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            RunNetsh($"advfirewall firewall delete rule name=\"{name}\"");
        }
        catch
        {
            // best effort
        }
    }

    /// <summary>
    ///     清理所有已添加的规则。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var rule in _rules)
            DeleteRule(rule);

        _rules.Clear();
    }

    private static (int ExitCode, string Output) RunNetsh(string args)
    {
        var psi = new ProcessStartInfo("netsh", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.GetEncoding(936), // GBK
            StandardErrorEncoding = Encoding.GetEncoding(936)
        };

        using var proc = Process.Start(psi);
        if (proc is null) return (-1, "");

        proc.WaitForExit(15000);
        var output = proc.StandardOutput.ReadToEnd();
        return (proc.ExitCode, output);
    }
}
