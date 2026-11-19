namespace PCL.UI.Bridges;

public sealed class LogFilterBridge : ILogFilter
{
    public string FilterUserName(string raw, char filterChar)
    {
        return SensitiveDataFilter.FilterUserName(raw, filterChar);
    }

    public string FilterAccessToken(string raw, char filterChar)
    {
        var login = LogFilter.CurrentLogin;
        return SensitiveDataFilter.FilterAccessToken(
            raw,
            filterChar,
            login?.AccessToken,
            login?.Uuid);
    }
}