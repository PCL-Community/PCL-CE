using PCL.Core.App;
using PCL.Core.Utils.Exts;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Net.Http.Client.Request;

public static class HttpSenderExtension
{
    extension (HttpRequestMessage requestMessage)
    {
        public async Task<HttpResponseMessage> SendAsync(HttpClient? httpClient = null, bool withLauncherMetadata = true, HttpCompletionOption httpCompletionOption = HttpCompletionOption.ResponseContentRead, int retryTimes = 3, CancellationToken cancellationToken = default)
        {
            using var request = requestMessage;
            httpClient ??= NetworkService.GetClient();

            if (withLauncherMetadata)
            {
                requestMessage.Headers.TryAddWithoutValidation("User-Agent", $"PCL-Community/PCL2-CE/{Basics.VersionName} (pclc.cc)");
                requestMessage.Headers.TryAddWithoutValidation("Referer", $"https://{Basics.VersionCode}.ce.open.pcl2.server/");
            }

            return await NetworkService.GetRetryPolicy(retryTimes)
                .ExecuteAsync(async token =>
                {
                    using var requestCopy = await request
                        .CloneAsync()
                        .ConfigureAwait(false);
                    return await httpClient
                        .SendAsync(requestCopy, httpCompletionOption, token)
                        .ConfigureAwait(false);
                }, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
