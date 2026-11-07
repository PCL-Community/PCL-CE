using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>解析 KDE Plasma 的 kioslaverc 手动代理配置。</summary>
public static class KdeProxyConfigParser
{
    public static IReadOnlyList<SystemProxyEntry> Parse(string configText)
    {
        if (string.IsNullOrWhiteSpace(configText)) return [];

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inProxySection = false;

        foreach (var rawLine in configText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inProxySection = line[1..^1]
                    .Trim()
                    .Equals("Proxy Settings", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inProxySection) continue;
            var index = line.IndexOf('=');
            if (index <= 0) continue;
            values[line[..index].Trim()] = line[(index + 1)..].Trim();
        }

        if (!values.TryGetValue("ProxyType", out var type)
            || !int.TryParse(type, out var typeValue)
            || typeValue != 1)
            return [];

        var proxies = new List<SystemProxyEntry>();
        Add("http", "httpProxy");
        Add("https", "httpsProxy");
        Add("socks", "socksProxy");
        return proxies;

        void Add(string protocol, string key)
        {
            if (!values.TryGetValue(key, out var value)) return;
            var address = Normalize(value);
            if (address is not null) proxies.Add(new SystemProxyEntry(protocol, address));
        }
    }

    public static string? Normalize(string value)
    {
        value = value.Trim();
        if (value.Length == 0) return null;

        if (value.Contains("://", StringComparison.Ordinal))
            value = value[(value.IndexOf("://", StringComparison.Ordinal) + 3)..];

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0]}:{parts[1]}" : value;
    }
}