using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Infrastructure;

public static class RetryHandler
{
    public static async Task<HttpResponseMessage> ExecuteWithRetryAsync(
        Func<Task<HttpResponseMessage>> sendAsync,
        int maxRetries = 3,
        CancellationToken ct = default)
    {
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var response = await sendAsync().ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return response;

            if (attempt < maxRetries && _ShouldRetry(response.StatusCode))
            {
                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000);
                await Task.Delay(delay, ct).ConfigureAwait(false);
                continue;
            }

            return response;
        }

        throw new InvalidOperationException("Retry logic reached unreachable state.");
    }

    private static bool _ShouldRetry(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout
            or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
    }
}
