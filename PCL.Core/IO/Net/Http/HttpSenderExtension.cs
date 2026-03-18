using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Net.Http.Client.Request;

public static class HttpSenderExtension
{
    extension(HttpRequestMessage requestMessage)
    {
        public async Task<HttpResponseMessage> SendAsync(
            HttpClient? httpClient = null,
            bool addMetedata = true,
            bool enableLogging = true,
            HttpCompletionOption httpCompletionOption = HttpCompletionOption.ResponseContentRead,
            int retryTimes = 3,
            CancellationToken cancellationToken = default)
        {
            using var request = requestMessage;
            httpClient ??= NetworkService.GetClient();

            if(addMetedata)
            {
                request
                    .WithHeader("User-Agent", $"PCL-Community/PCL2-CE/{Basics.VersionName} (pclc.cc)")
                    .WithHeader("Referer", $"https://{Basics.VersionCode}.ce.open.pcl2.server/");
            }

            var requestId = Guid.NewGuid().ToString();
            if (enableLogging)
                LogWrapper.Info(
                    "Request",
                    $"Send request to {request.RequestUri} (method = {request.Method}, id = {requestId})");

            var resp = await NetworkService.GetRetryPolicy(retryTimes)
                .ExecuteAsync(
                    async token =>
                    {
                        if (enableLogging)
                            LogWrapper.Debug("Request", $"Try attempt (id = {requestId})");
                        try
                        {
                            using var requestCopy = await request
                                .CloneAsync()
                                .ConfigureAwait(false);
                            return await httpClient
                                .SendAsync(requestCopy, httpCompletionOption, token)
                                .ConfigureAwait(false);
                        } 
                        catch(Exception ex)
                        {
                            LogWrapper.Error(ex, "Request", $"Try attempt failed (id = {requestId})");
                            throw;
                        }
                    },
                    cancellationToken,
                    false)
                .ConfigureAwait(false);

            if (enableLogging)
                LogWrapper.Info("Request", $"End request, got http status code {resp.StatusCode} (id = {requestId})");
            return resp;
        }
    }
}
