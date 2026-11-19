namespace PCL;

public static class SensitiveDataFilter
{
    public static string FilterUserName(string raw, char filterChar)
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

    public static string FilterAccessToken(
        string raw,
        char filterChar,
        string? knownToken = null,
        string? knownUuid = null)
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

        if (knownToken is not { Length: >= 10 }
            || !raw.Contains(knownToken, StringComparison.OrdinalIgnoreCase)
            || string.Equals(knownUuid, knownToken, StringComparison.OrdinalIgnoreCase))
            return raw;

        return raw.Replace(
            knownToken,
            knownToken[..5]
            + new string(filterChar, knownToken.Length - 10)
            + knownToken[^5..]);
    }
}