using PCL.Core.App;
using System.Net.Http;
using System.Threading.Tasks;

namespace PCL.Core.IO.Net.Http.Client.Request;

public static class HttpSender
{
    extension (HttpRequestMessage requestMessage)
    {
        public async Task<HttpResponseMessage> SendAsync(bool withLauncherMetadata = true)
        {
            if (withLauncherMetadata)
            {
                requestMessage.Headers.TryAddWithoutValidation("User-Agent", $"PCL-Community/PCL2-CE/{Basics.VersionName} (pclc.cc)");
                requestMessage.Headers.TryAddWithoutValidation("Referer", $"https://{Basics.VersionCode}.ce.open.pcl2.server/");
            }

            return await NetworkService.GetClient().SendAsync(requestMessage);
        }
    }
}
