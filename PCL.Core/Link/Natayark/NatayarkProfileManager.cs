using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.Utils.OS;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using PCL.Core.IO.Net.Http;
using PCL.Core.Utils;

namespace PCL.Core.Link.Natayark;

public class NaidUser
{
    public int Id { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    /// <summary>
    /// Natayark ID 状态，1 为正常
    /// </summary>
    public int Status { get; set; }
    public bool IsRealNamed { get; set; }
    public string? LastIp { get; set; }

}

public static class NatayarkProfileManager
{
    private const string LogModule = "Link";

    public static NaidUser NaidProfile { get; private set; } = new();

    private static Task? _getNaidData;

    public static async Task GetNaidDataAsync(string token, bool isRefresh = false, bool isRetry = false, ushort port = 0)
    {
        if (_getNaidData is not null && !_getNaidData.IsCompleted)
        {
            await _getNaidData;
            return;
        }
        _getNaidData = Task.Run(async () =>
        {
            try
            {
                // 获取 AccessToken 和 RefreshToken
                var requestData =
                    $"grant_type={(isRefresh ? "refresh_token" : "authorization_code")}" +
                    $"&client_id={EnvironmentInterop.GetSecret("NAID_CLIENT_ID")}" +
                    $"&client_secret={EnvironmentInterop.GetSecret("NAID_CLIENT_SECRET")}" +
                    $"&{(isRefresh ? "refresh_token" : "code")}={token}" +
                    (isRefresh ? "" : $"&redirect_uri=http://localhost:{port}/callback");

                var httpContent = new StringContent(requestData, Encoding.UTF8, "application/x-www-form-urlencoded");

                using var oauthResponse = await HttpRequest
                    .CreatePost("https://account.naids.com/api/oauth2/token")
                    .WithContent(httpContent)
                    .SendAsync()
                    .ConfigureAwait(false);
                oauthResponse.EnsureSuccessStatusCode();

                var result = await oauthResponse.AsStringAsync().ConfigureAwait(false) 
                    ?? throw new Exception(Lang.Text("Link.Natayark.TokenFetchEmpty"));
                var data = JsonCompat.ParseNode(result);
                var accessToken = data?["access_token"]?.ToString();
                var refreshToken = data?["refresh_token"]?.ToString();

                if (data is null || accessToken is null || refreshToken is null)
                    throw new Exception(Lang.Text("Link.Natayark.TokenParseFailed"));

                NaidProfile.AccessToken = accessToken;
                NaidProfile.RefreshToken = refreshToken;

                var expiresAt = data["refresh_token_expires_at"]!.ToString();

                // 获取用户信息
                using var userDataResponse = await HttpRequest
                    .Create("https://account.naids.com/api/api/user/data")
                    .WithBearerToken(NaidProfile.AccessToken)
                    .SendAsync()
                    .ConfigureAwait(false);
                userDataResponse.EnsureSuccessStatusCode();

                var receivedUserData = await userDataResponse.AsStringAsync()
                    ?? throw new Exception(Lang.Text("Link.Natayark.UserInfoFetchEmpty"));
                var userData = (JsonCompat.ParseNode(receivedUserData)?["data"])
                    ?? throw new Exception(Lang.Text("Link.Natayark.UserInfoParseFailed"));

                NaidProfile.Id = JsonCompat.ToObject<int>(userData["id"]);
                NaidProfile.Username = userData["username"]?.ToString() ?? string.Empty;
                NaidProfile.Email = userData["email"]?.ToString() ?? string.Empty;
                NaidProfile.Status = JsonCompat.ToObject<int>(userData["status"]);
                NaidProfile.IsRealNamed = JsonCompat.ToObject<bool>(userData["realname"]);
                NaidProfile.LastIp = userData["last_ip"]?.ToString() ?? string.Empty;

                // 保存数据
                States.Link.NaidRefreshToken = NaidProfile.RefreshToken;
                States.Link.NaidRefreshExpireTime = expiresAt;
            }
            catch (Exception ex)
            {
                if (isRetry)
                {
                    NaidProfile = new NaidUser();
                    States.Link.NaidRefreshToken = string.Empty;
                    WarnLog(Lang.Text("Tools.GameLink.Natayark.ProfileLoadFailed"));
                }
                else
                {
                    if (ex.Message.Contains("invalid access token"))
                    {
                        WarnLog(Lang.Text("Tools.GameLink.Natayark.TokenInvalid"));
                        await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false); // 搁这让电脑休息半秒吗
                        await GetNaidDataAsync(States.Link.NaidRefreshToken, true, true).ConfigureAwait(false);
                    }
                    else if (ex.Message.Contains("invalid_grant"))
                    {
                        WarnLog(Lang.Text("Tools.GameLink.Natayark.InvalidAuthCode"));
                    }
                    else if (ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized })
                    {
                        NaidProfile = new NaidUser();
                        States.Link.NaidRefreshToken = string.Empty;
                        WarnLog(Lang.Text("Tools.GameLink.Natayark.AccountExpired"));
                    }
                    else
                    {
                        NaidProfile = new NaidUser();
                        States.Link.NaidRefreshToken = string.Empty;
                        WarnLog(Lang.Text("Tools.GameLink.Natayark.LoginFailed"));
                    }
                }
                throw;

                void WarnLog(string msg)
                {
                    LogWrapper.Warn(ex, LogModule, msg);
                    HintWrapper.Show(msg, HintTheme.Error);
                }
            }
        });

        await _getNaidData;
    }
}
