using System.Text.RegularExpressions;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>解析 <c>scutil --proxy</c> 输出。</summary>
public static class MacOsProxyConfigParser
{
    public static IReadOnlyList<SystemProxyEntry> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var proxies = new List<SystemProxyEntry>();
        Add("http", "HTTPEnable", "HTTPProxy", "HTTPPort");
        Add("https", "HTTPSEnable", "HTTPSProxy", "HTTPSPort");
        Add("socks", "SOCKSEnable", "SOCKSProxy", "SOCKSPort");
        return proxies;

        void Add(
            string protocol,
            string enableKey,
            string hostKey,
            string portKey)
        {
            if (_ReadInt(text, enableKey) != 1) return;
            var host = _ReadString(text, hostKey);
            if (string.IsNullOrWhiteSpace(host)) return;
            var port = _ReadInt(text, portKey);
            proxies.Add(
                new SystemProxyEntry(
                    protocol,
                    port is > 0 and <= 65535 ? $"{host}:{port}" : host));
        }
    }

    private static int? _ReadInt(string text, string key)
    {
        var match = Regex.Match(
            text,
            $@"^\s*{Regex.Escape(key)}\s*:\s*(\d+)\s*$",
            RegexOptions.Multiline);
        return match.Success && int.TryParse(match.Groups[1].Value, out var value)
            ? value
            : null;
    }

    private static string? _ReadString(string text, string key)
    {
        var match = Regex.Match(
            text,
            $@"^\s*{Regex.Escape(key)}\s*:\s*(.+?)\s*$",
            RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim().Trim('"') : null;
    }
}