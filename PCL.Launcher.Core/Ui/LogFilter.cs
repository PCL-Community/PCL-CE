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
