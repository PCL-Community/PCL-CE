namespace PCL;

/// <summary>日志中敏感信息的打码。</summary>
public interface ILogFilter
{
    string FilterUserName(string raw, char filterChar);
    string FilterAccessToken(string raw, char filterChar);
}

public static class LogFilter
{
    public static ILogFilter? Current { get; set; }

    public static string FilterUserName(string raw, char filterChar)
    {
        return Current?.FilterUserName(raw, filterChar) ?? raw;
    }

    public static string FilterAccessToken(string raw, char filterChar)
    {
        return Current?.FilterAccessToken(raw, filterChar) ?? raw;
    }
}

/// <summary>区域限制功能判定。</summary>
public interface IRestrictedFeatures
{
    bool IsRestricted { get; }
    bool Check(string feature);
    void ThrowIfRestricted(string feature);
}

public static class RestrictedFeatures
{
    public static IRestrictedFeatures? Current { get; set; }
    public static bool IsRestricted => Current?.IsRestricted ?? false;

    public static bool Check(string feature)
    {
        return Current?.Check(feature) ?? true;
    }

    public static void ThrowIfRestricted(string feature)
    {
        Current?.ThrowIfRestricted(feature);
    }
}