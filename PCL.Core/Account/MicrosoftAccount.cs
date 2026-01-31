using System;
using System.Net.Http;
using System.Security;
using PCL.Core.App;
using PCL.Core.Net.Http.Client;

namespace PCL.Core.Account;

public class MicrosoftAccount : IAccount
{
    public string ServiceName => "Microsoft";
    public string Uuid { get; }
    public string Name { get; }
    public string Username { get; }
    public SecureString Password { get; }
    public string OAuthAddress { get; }
    public string OAuthClientId => Secrets.MSOAuthClientId;
    public SecureString AccessToken { get; }
    public SecureString RefreshToken { get; }
    public string ExpireTime { get; }
    
    public MicrosoftAccount(string uuid, string name, string username, SecureString password, string oAuthAddress, SecureString accessToken, SecureString refreshToken)
    {
        Uuid = uuid;
        Name = name;
        Username = username;
        Password = password;
        OAuthAddress = oAuthAddress;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
    }

    public bool OAuthRefresh()
    {
        try
        {
            string result;
            using (var response = HttpRequestBuilder.Create("https://login.live.com/oauth20_token.srf", HttpMethod.Post)
                       .WithContent(
                           $"client_id={OAuthClientId}&refresh_token={Uri.EscapeDataString(RefreshToken.ToString())}&grant_type=refresh_token&scope=XboxLive.signin%20offline_access",
                           "application/x-www-form-urlencoded").SendAsync(true).GetAwaiter().GetResult())
            {
                result = response.AsStringContent();
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}