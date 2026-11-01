using System.Net;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;

namespace PCL;

public static partial class WebServer
{
    public class NaidCallbackServer(
        string serviceName,
        OAuthComplete completeCallback,
        string? picAddress)
        : HttpServer([IPAddress.Parse("127.0.0.1")])
    {
        private string? _callbackContent;
        private IDictionary<string, string>? _callbackParameters;

        private OAuthCompleteStatus? _status;

        protected override void Init()
        {
            Register(HttpMethod.Get, "/callback", HandleCallback);
            Register(HttpMethod.Post, "/callback", HandleCallback);
            Register(HttpMethod.Get, "/status", HandleStatus);
            Register(HttpMethod.Get, "/assets/background", HandleBackground);
            Register(HttpMethod.Get, "/assets/icon", HandleIcon);
            Register(HttpMethod.Get, "/complete", HandleComplete);
        }

        private Task<HttpRouteResponse> HandleCallback(HttpListenerRequest request)
        {
            if (!request.IsLocal)
                return HttpRouteResponse.Forbidden.AsTask();

            var redirect = HttpRouteResponse.Redirect("/complete").AsTask();

            var parameterMap = new Dictionary<string, string>();
            var query = request.Url?.Query ?? string.Empty;
            var queryIndex = query.IndexOf('?');
            if (queryIndex != -1 && query.Length > queryIndex)
                try
                {
                    var sq = query[(queryIndex + 1)..].Split('&');
                    var splitChar = new[]
                    {
                        '='
                    };
                    foreach (var iq in sq)
                    {
                        var q = iq.Split(splitChar, 2);
                        if (q.Length == 2) parameterMap[q[0]] = q[1];
                    }
                }
                catch (Exception ex)
                {
                    _status = OAuthCompleteStatus.Failed(
                        Lang.Text("Launch.Account.Auth.OAuth.CallbackParseFailed"),
                        ex);
                    return redirect;
                }

            _callbackParameters = parameterMap;

            if (request.HasEntityBody)
                try
                {
                    using var reader = new StreamReader(
                        request.InputStream,
                        request.ContentEncoding);
                    _callbackContent = reader.ReadToEnd();
                }
                catch (Exception ex)
                {
                    _status = OAuthCompleteStatus.Failed(
                        Lang.Text("Launch.Account.Auth.OAuth.CallbackReadFailed"),
                        ex);
                    return redirect;
                }

            return redirect;
        }

        private Task<HttpRouteResponse> HandleStatus(HttpListenerRequest request)
        {
            if (_callbackParameters is null)
                return HttpRouteResponse.NotFound.AsTask();

            try
            {
                if (_status is null)
                {
                    _callbackParameters["Port"] = Port.ToString();
                    _status = completeCallback(
                        true,
                        _callbackParameters,
                        _callbackContent ?? string.Empty);
                }
                else if (!_status.Success)
                {
                    LauncherCommon.Log(
                        $"[OAuth] {serviceName}: {_status.Message}\r\n{_status.Stacktrace}");
                    var pa = new Dictionary<string, string>
                    {
                        ["Port"] = Port.ToString()
                    };
                    completeCallback(false, pa, _status.Message ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                _status = OAuthCompleteStatus.Failed(
                    Lang.Text("Launch.Account.Auth.OAuth.CallbackHandleFailed"),
                    ex);
            }

            return HttpRouteResponse
                .Json(
                    _status
                    ?? OAuthCompleteStatus.Failed(
                        Lang.Text("Launch.Account.Auth.OAuth.CallbackHandleFailed")))
                .AsTask();
        }

        private Task<HttpRouteResponse> HandleBackground(HttpListenerRequest request)
        {
            if (string.IsNullOrWhiteSpace(picAddress))
                return Task.FromResult(HttpRouteResponse.NotFound);
            return HttpRouteResponse
                .Input(
                    new FileStream(
                        picAddress,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.None,
                        16384,
                        true))
                .AsTask();
        }

        private static Task<HttpRouteResponse> HandleIcon(HttpListenerRequest request)
        {
            return HttpRouteResponse
                .Input(
                    LauncherCommon.GetResourceStream("Images/icon.ico")
                    ?? throw new FileNotFoundException("无法找到内置图标资源"))
                .AsTask();
        }

        private static Task<HttpRouteResponse> HandleComplete(HttpListenerRequest request)
        {
            return HttpRouteResponse
                .Input(
                    LauncherCommon.GetResourceStream("Resources/oauth-complete.html")
                    ?? throw new FileNotFoundException("无法找到内置回调页面资源"),
                    "text/html")
                .AsTask();
        }
    }
}