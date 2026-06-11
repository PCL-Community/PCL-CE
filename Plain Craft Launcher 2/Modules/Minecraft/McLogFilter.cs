using System;
using PCL;

namespace PCL
{
    public static class McLogFilter
    {
        /// <summary>
        ///     打码字符串中的 AccessToken。
        /// </summary>
        public static string FilterAccessToken(string raw, char filterChar)
        {
            // 打码 "accessToken " 后的内容
            if (raw.Contains("accessToken "))
                foreach (var Token in raw.RegexSearch("(?<=accessToken ([^ ]{5}))[^ ]+(?=[^ ]{5})"))
                    raw = raw.Replace(Token, new string(filterChar, Token.Count()));
            // 打码当前登录的结果
            var accessToken = ModLaunch.mcLoginLoader.output.AccessToken;
            if (accessToken is not null && accessToken.Length >= 10 && raw.ContainsF(accessToken, true) &&
                (ModLaunch.mcLoginLoader.output.Uuid ?? "") !=
                (ModLaunch.mcLoginLoader.output.AccessToken ?? "")) // UUID 和 AccessToken 一样则不打码
                raw = raw.Replace(accessToken, accessToken[..5] + new string(filterChar, accessToken.Length - 10) +
                                               accessToken[^5..]);
            return raw;
        }

        /// <summary>
        ///     打码字符串中的 Windows 用户名。
        /// </summary>
        public static string FilterUserName(string raw, char filterChar)
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var userName = userProfile.Split(@"\").Last();
            var maskedProfile = userProfile.Replace(userName, new string(filterChar, userName.Length));
            return raw.Replace(userProfile, maskedProfile);
        }
    }
}
