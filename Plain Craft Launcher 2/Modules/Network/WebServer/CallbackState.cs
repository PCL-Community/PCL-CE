using PCL.Core.App;
using PCL.Core.Link.Natayark;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;

namespace PCL;

public static partial class WebServer
{
    public delegate OAuthCompleteStatus? OAuthComplete(
        bool success,
        IDictionary<string, string> parameters,
        string content);

    private static readonly Lock ChangeLock = new();
    private static string? _picAddress;

    public static object BackgroundPicChangeCallback(string pic)
    {
        lock (ChangeLock)
        {
            _picAddress = pic;
            return true;
        }
    }

    public static bool StartOAuthWaitingCallback(
        string serviceName,
        string url,
        OAuthComplete completeCallback)
    {
        if (IsWebServerRunning(serviceName))
            return false;
        Common.RunInNewThread(
            () =>
            {
                int serverPort;
                lock (WebServers)
                {
                    string? currentPicAddress;
                    lock (ChangeLock)
                    {
                        currentPicAddress = _picAddress;
                    }

                    var server = new NaidCallbackServer(
                        serviceName,
                        completeCallback,
                        currentPicAddress);
                    serverPort = server.Port;
                    Common.Log($"[OAuth] {serviceName}: 已开始监听 {server.Port} 端口，正在初始化路由");
                    var webServiceName = $"oauth/{serviceName}";
                    if (DisposeWebServer(webServiceName)) Common.Log("[OAuth] 已关闭先前认证服务服务端");
                    StartWebServer(webServiceName, server);
                    Common.Log($"[OAuth] {serviceName}: 初始化完成，开始响应 HTTP 请求");
                }

                Common.OpenWebsite(url.Replace("%r", $"http://localhost:{serverPort}/callback"));
            },
            $"CallbackWebServerLoading/{serviceName}");
        return true;
    }

    public static void StartNaidAuthorize(Action? completeCallback = null)
    {
        Exception? resultEx = null;
        StartOAuthWaitingCallback(
            "NatayarkID",
            $"https://account.naids.com/oauth2/authorize?response_type=code&client_id={Secrets.NatayarkClientId}&redirect_uri=%r",
            (success, parameters, content) =>
            {
                OAuthCompleteStatus? status;
                if (!success)
                {
                    DialogService.Instance.Show(new TextDialogRequest
                    {
                        Title = Lang.Text("Common.Dialog.Title"),
                        Content = content,
                        Buttons = DialogButtons.Ok,
                        IsWarn = true,
                        ForceWait = true
                    });
                    completeCallback?.Invoke();
                    return null;
                }

                var code = parameters["code"];

                try
                {
                    NatayarkProfileManager
                        .GetNaidDataAsync(code, port: ushort.Parse(parameters["Port"]))
                        .Wait();
                }
                catch (AggregateException ex)
                {
                    resultEx = ex.InnerExceptions[0];
                }

                if (resultEx is null)
                    status = OAuthCompleteStatus.Complete(
                        NatayarkProfileManager.NaidProfile.Username);
                else
                    status = OAuthCompleteStatus.Failed(Lang.Text("Launch.Account.Auth.OAuth.ProfileFetchFailed"), resultEx);
                completeCallback?.Invoke();
                return status;
            });
    }

    public class OAuthCompleteStatus
    {
        public bool Success { get; set; }
        public string? Username { get; set; }
        public string? Message { get; set; }
        public string? Stacktrace { get; set; }

        public static OAuthCompleteStatus Complete(string? username)
        {
            return new OAuthCompleteStatus
            {
                Success = true,
                Username = username
            };
        }

        public static OAuthCompleteStatus Failed(string message, Exception? ex = null)
        {
            var init = new OAuthCompleteStatus();
            return (
                init.Success = false,
                init.Message = message,
                init.Stacktrace = ex?.ToString(),
                init).init;
        }
    }
}
