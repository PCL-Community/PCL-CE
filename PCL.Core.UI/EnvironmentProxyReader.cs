using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>所有平台共享的代理环境变量读取。</summary>
internal static class EnvironmentProxyReader
{
    public static IReadOnlyList<SystemProxyEntry> Read()
    {
        var result = new List<SystemProxyEntry>();
        Add("http", ["http_proxy", "HTTP_PROXY"]);
        Add("https", ["https_proxy", "HTTPS_PROXY"]);
        Add("socks", ["all_proxy", "ALL_PROXY"]);
        return result;

        void Add(string protocol, IEnumerable<string> names)
        {
            var value = names
                .Select(Environment.GetEnvironmentVariable)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(new SystemProxyEntry(protocol, value.Trim()));
        }
    }
}