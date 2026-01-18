using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Net;
using PCL.Core.UI;
using PCL.Core.Utils.OS;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using PCL.Core.Net.Http.Client;

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
        if (_getNaidData != null && !_getNaidData.IsCompleted)
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

                using var oauthResponse = await HttpRequestBuilder
                    .Create("https://account.naids.com/api/oauth2/token", HttpMethod.Post)
                    .WithContent(httpContent)
                    .SendAsync(true).ConfigureAwait(false);

                var result = await oauthResponse.AsStringAsync().ConfigureAwait(false);

                if (result == null) throw new Exception("获取 AccessToken 与 RefreshToken 失败，返回内容为空");

                var data = JsonNode.Parse(result);
                var accessToken = data?["access_token"]?.ToString();
                var refreshToken = data?["refresh_token"]?.ToString();

                if (data == null || accessToken == null || refreshToken == null)
                    throw new Exception("获取 AccessToken 与 RefreshToken 失败，解析返回内容失败");

                NaidProfile.AccessToken = accessToken;
                NaidProfile.RefreshToken = refreshToken;

                var expiresAt = data["refresh_token_expires_at"]!.ToString();

                // 获取用户信息
                using var userDataResponse = await HttpRequestBuilder
                    .Create("https://account.naids.com/api/api/user/data", HttpMethod.Get)
                    .WithBearerToken(NaidProfile.AccessToken)
                    .SendAsync(true).ConfigureAwait(false);

                var receivedUserData = await userDataResponse.AsStringAsync();
                if (receivedUserData == null) throw new Exception("获取 Natayark 用户信息失败，返回内容为空");

                var userData = JsonNode.Parse(receivedUserData)?["data"];
                if (userData == null) throw new Exception("获取 Natayark 用户信息失败，解析返回内容失败");

                NaidProfile.Id = userData["id"]?.GetValue<int>() ?? 0;
                NaidProfile.Username = userData["username"]?.ToString() ?? string.Empty;
                NaidProfile.Email = userData["email"]?.ToString() ?? string.Empty;
                NaidProfile.Status = userData["status"]?.GetValue<int>() ?? 0;
                NaidProfile.IsRealNamed = userData["realname"]?.GetValue<bool>() ?? false;
                NaidProfile.LastIp = userData["last_ip"]?.ToString() ?? string.Empty;

                // 保存数据
                Config.Link.NaidRefreshToken = NaidProfile.RefreshToken;
                Config.Link.NaidRefreshExpireTime = expiresAt;
            }
            catch (Exception ex)
            {
                if (isRetry)
                {
                    NaidProfile = new NaidUser();
                    Config.Link.NaidRefreshToken = string.Empty;
                    WarnLog("获取 Natayark 用户数据失败，请尝试前往设置重新登录");
                }
                else
                {
                    if (ex.Message.Contains("invalid access token"))
                    {
                        WarnLog("Naid Access Token 无效，尝试刷新登录");
                        await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false); // 搁这让电脑休息半秒吗
                        await GetNaidDataAsync(Config.Link.NaidRefreshToken, true, true).ConfigureAwait(false);
                    }
                    else if (ex.Message.Contains("invalid_grant"))
                    {
                        WarnLog("Naid 验证代码无效");
                    }
                    else if (ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized })
                    {
                        NaidProfile = new NaidUser();
                        Config.Link.NaidRefreshToken = string.Empty;
                        WarnLog("Natayark 账号信息已过期，请前往设置重新登录！");
                    }
                    else
                    {
                        NaidProfile = new NaidUser();
                        Config.Link.NaidRefreshToken = string.Empty;
                        WarnLog("Naid 登录失败，请尝试前往设置重新登录");
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
