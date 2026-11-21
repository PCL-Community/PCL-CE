using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>所有平台共享的代理环境变量读取。</summary>
internal static class EnvironmentProxyReader
{
    public static IReadOnlyList<SystemProxyEntry> Read()
    {
        var env = ReadRaw();
        var result = new List<SystemProxyEntry>();
        Add("http", env.Http);
        Add("https", env.Https);
        Add("socks", env.All);
        return result;

        void Add(string protocol, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(new SystemProxyEntry(protocol, value));
        }
    }

    /// <summary>读取原始代理环境变量，由各平台决定回退策略。</summary>
    public static ProxyEnvironmentValues ReadRaw()
    {
        return new ProxyEnvironmentValues(
            ReadFirst("http_proxy", "HTTP_PROXY"),
            ReadFirst("https_proxy", "HTTPS_PROXY"),
            ReadFirst("all_proxy", "ALL_PROXY"));
    }

    private static string? ReadFirst(params string[] names)
    {
        return (
            from name in names
            select Environment.GetEnvironmentVariable(name)
            into value
            where !string.IsNullOrWhiteSpace(value)
            select value.Trim()).FirstOrDefault();
    }
}

internal readonly record struct ProxyEnvironmentValues(
    string? Http,
    string? Https,
    string? All);