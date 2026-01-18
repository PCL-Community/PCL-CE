using PCL.Core.Logging;
using PCL.Core.Net.Http.Client;
using PCL.Core.Net.Http.Server;
using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Account.OAuth;

public class MicrosoftCodeFlowOAuthSession(string clientId, string scope) : LoginSession<MicrosoftCodeFlowOAuth>
{
    private class OAuthServer() : HttpServer([IPAddress.Loopback])
    {
        public string? Code = null;
        public string? State = null;
        private readonly TaskCompletionSource _tcs = new();
        public Task WaitForCallbackAsync() => _tcs.Task;

        protected override void Init()
        {
            Register(HttpMethod.Get, "/oauth/callback", _OAuthCallback);
            Register(HttpMethod.Post, "/oauth/callback", _OAuthCallback);
            Register(HttpMethod.Get, "/oauth/success", _SuccessPage);
        }

        private async Task<HttpRouteResponse> _OAuthCallback(HttpListenerRequest request)
        {
            if (!request.IsLocal) return HttpRouteResponse.Forbidden;
            if (request.QueryString.Get("error") == null)
            {
                var code = request.QueryString.Get("code");
                var inputState = request.QueryString.Get("state");
                if (State != null && inputState != State) return HttpRouteResponse.BadRequest;
                return HttpRouteResponse.Redirect("/oauth/success");
            }
            else
            {
                var errorMsg = $"{request.QueryString.Get("error")}: {request.QueryString.Get("error_description")}";
                _tcs.TrySetException(new Exception(errorMsg));
                return HttpRouteResponse.Text(errorMsg);
            }
        }

        private async Task<HttpRouteResponse> _SuccessPage(HttpListenerRequest request)
        {
            if (!request.IsLocal) return HttpRouteResponse.Forbidden;
            if (Code == null) return HttpRouteResponse.BadRequest;
            _tcs.TrySetResult();
            return HttpRouteResponse.Text("认证成功，请返回启动器");
        }

        public new void Stop()
        {
            _tcs.TrySetCanceled();
            base.Stop();
        }
    }
    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "Bearer";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string Scope { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }
    }

    private readonly OAuthServer _server = new();

    public override async Task BeginAsync()
    {
        try
        {
            await Task.Run(() => OnStateChanged(AuthStep.Initializing));
            var challenge = PKCE.GenerateChallenge();
            var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            _server.State = state;
            var encodedScope = Uri.EscapeDataString(scope);
            this.AuthUrl =
                $"https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize?client_id={clientId}&response_type=code&redirect_uri=http://127.0.0.1:{_server.Port}/oauth/callback&response_mode=query&scope={encodedScope}&state={state}&code_challenge={challenge.Challenge}&code_challenge_method={challenge.Method}";
            await Task.Run(() => OnStateChanged(AuthStep.PendingUser));
            await _server.WaitForCallbackAsync();
            await Task.Run(() => OnStateChanged(AuthStep.GettingCode));
            var queryCtx = $"client_id={clientId}" +
                $"&scope={encodedScope}" +
                $"&code={_server.Code}" +
                $"&redirect_uri=http://127.0.0.1:{_server.Port}/oauth/callback" +
                $"&grant_type=authorization_code" +
                $"&code_verifier={challenge.Verifier}";
            using var resp = await HttpRequestBuilder.Create($"https://login.microsoftonline.com/consumers/oauth2/v2.0/token", HttpMethod.Post)
                .WithContent(queryCtx, "application/x-www-form-urlencoded")
                .SendAsync(true);
            var ret = await resp.AsJsonAsync<TokenResponse>();
            if (ret == null) throw new Exception("无法解析服务器结果");
            this.AccessToken = ret.AccessToken;
            this.RefreshToken = ret.RefreshToken;
            this.ExpireIn = ret.ExpiresIn;
            await Task.Run(() => OnStateChanged(AuthStep.Success));
            _tcs.TrySetResult(new MicrosoftCodeFlowOAuth());
        }
        catch (Exception ex)
        {
            await Task.Run(() => OnStateChanged(AuthStep.Error));
            LogWrapper.Error(ex, "MsCodeOAuth", "流程出现错误");
        }
        finally
        {
            _server.Stop();
        }
    }
}