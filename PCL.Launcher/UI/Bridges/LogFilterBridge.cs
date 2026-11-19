namespace PCL.UI.Bridges;

/// <summary>业务层日志打码：遮盖用户目录中的用户名，并把 AccessToken 首尾 5 位之外的部分替换为掩码字符。</summary>
public sealed class LogFilterBridge : ILogFilter
{
    public string FilterUserName(string raw, char filterChar)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userProfile) || !raw.Contains(userProfile))
            return raw;

        var userName = Path.GetFileName(userProfile);
        if (string.IsNullOrEmpty(userName))
            return raw;

        return raw.Replace(
            userProfile,
            userProfile.Replace(userName, new string(filterChar, userName.Length)));
    }

    public string FilterAccessToken(string raw, char filterChar)
    {
        const string marker = "accessToken ";
        var index = raw.IndexOf(marker, StringComparison.Ordinal);
        while (index >= 0)
        {
            var start = index + marker.Length;
            var end = raw.IndexOf(' ', start);
            if (end < 0) end = raw.Length;
            var length = end - start;
            if (length > 10)
            {
                var token = raw[start..end];
                raw = raw[..start]
                    + token[..5]
                    + new string(filterChar, length - 10)
                    + token[^5..]
                    + raw[end..];
                end = start + length;
            }

            index = raw.IndexOf(marker, end, StringComparison.Ordinal);
        }

        var login = LogFilter.CurrentLogin;
        var accessToken = login?.AccessToken;
        if (accessToken is { Length: >= 10 }
            && raw.Contains(accessToken, StringComparison.OrdinalIgnoreCase)
            && (login?.Uuid ?? "") != accessToken)
            raw = raw.Replace(
                accessToken,
                accessToken[..5]
                + new string(filterChar, accessToken.Length - 10)
                + accessToken[^5..]);

        return raw;
    }
}