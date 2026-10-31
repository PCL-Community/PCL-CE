using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using PCL.Core.IO.Net;
using PCL.Core.IO.Net.Http;

namespace PCL.Network;

public static class Requester
{
    public static void EnsureSuccess(HttpResponseMessage? response)
    {
        if (!response?.IsSuccessStatusCode ?? true)
            throw new HttpResponseException(response);
    }

    public static async Task<string> FetchStringAsync(string url, RequestParam param = default)
    {
        return await FetchAsync(url, new FetchParam
        {
            Method = "GET",
            Accept = param.Accept,
            FallbackUrl = param.FallbackUrl,
            UseBrowserUserAgent = param.UseBrowserUserAgent,
            Timeout = param.Timeout,
            Encoding = param.Encoding,
            MakeLog = true
        }, Math.Max(1, param.Retries == 0 ? 1 : param.Retries)).ConfigureAwait(false);
    }

    public static string FetchString(string url, RequestParam param = default)
    {
        return FetchStringAsync(url, param).GetAwaiter().GetResult();
    }

    public static async Task<JsonNode> FetchJsonAsync(string url, RequestParam param = default)
    {
        return Common.GetJson(await FetchStringAsync(url, param).ConfigureAwait(false));
    }

    public static async Task<T> FetchJsonAsync<T>(string url, RequestParam param = default) where T : JsonNode
    {
        return (T)await FetchJsonAsync(url, param).ConfigureAwait(false);
    }

    public static JsonNode FetchJson(string url, RequestParam param = default)
    {
        return FetchJsonAsync(url, param).GetAwaiter().GetResult();
    }

    public static T FetchJson<T>(string url, RequestParam param = default) where T : JsonNode
    {
        return FetchJsonAsync<T>(url, param).GetAwaiter().GetResult();
    }

    public static async Task<string> FetchAsync(string url, FetchParam param)
    {
        return await FetchAsync(url, param, 3).ConfigureAwait(false);
    }

    public static string Fetch(string url, FetchParam param)
    {
        return FetchAsync(url, param).GetAwaiter().GetResult();
    }

    public static string Fetch(string url)
    {
        return Fetch(url, new FetchParam { Method = "GET", Timeout = 30000, MakeLog = true });
    }

    private static async Task<string> FetchAsync(string url, FetchParam param, int retries)
    {
        var urls = new[] { url, param.FallbackUrl }.Where(u => !string.IsNullOrWhiteSpace(u)).Cast<string>().ToList();
        var maxAttempts = Math.Max(1, retries);
        Exception? lastException = null;
        foreach (var currentUrl in urls)
        {
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    return await FetchOnceAsync(currentUrl, param).ConfigureAwait(false);
                }
                catch (HttpResponseException ex) when (!IsTransient(ex))
                {
                    // 4xx 等确定性失败：同一地址重试不会有不同结果，直接换下一个地址
                    lastException = ex;
                    break;
                }
                catch (Exception ex) when (IsTransient(ex))
                {
                    lastException = ex;
                    if (attempt + 1 < maxAttempts)
                        await Task.Delay(_GetRetryDelay(attempt)).ConfigureAwait(false);
                }
            }
        }

        throw lastException ?? new HttpRequestException("请求失败");
    }

    /// <summary>
    /// 判断异常是否属于值得重试的瞬态故障。
    /// </summary>
    private static bool IsTransient(Exception ex) => ex switch
    {
        HttpResponseException { Response: not null } wrapped => IsTransientStatus(wrapped.Response!.StatusCode),
        HttpResponseException => true,
        HttpRequestException => true,
        TaskCanceledException => true,
        IOException => true,
        _ => false
    };

    private static bool IsTransientStatus(HttpStatusCode status) =>
        (int)status >= 500
        || status == HttpStatusCode.RequestTimeout
        || status == HttpStatusCode.TooManyRequests;

    /// <summary>
    /// 指数退避加抖动，避免故障期间大量客户端同步重试造成惊群。
    /// </summary>
    private static TimeSpan _GetRetryDelay(int attempt) =>
        TimeSpan.FromMilliseconds(
            Math.Min(4000, 500 * Math.Pow(2, attempt)) * (0.8 + Random.Shared.NextDouble() * 0.4));

    private static async Task<string> FetchOnceAsync(string url, FetchParam param)
    {
        HttpResponseMessage? response = null;
        var request = new HttpRequestMessage(ParseMethod(param.Method), RequestSigning.SecretCdnSign(url));
        RequestSigning.SecretHeadersSign(url, ref request, param.UseBrowserUserAgent);
        try
        {
            if (!string.IsNullOrWhiteSpace(param.Accept))
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(param.Accept));
            if (param.Headers is not null)
                foreach (var header in param.Headers)
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (SupportBody(request.Method) && param.Content is not null)
            {
                if (param.Content is HttpContent httpContent)
                {
                    request.Content = httpContent;
                }
                else
                {
                    var content = param.Content is string text ? text : param.Content.ToString() ?? "";
                    request.Content = new StringContent(content, param.Encoding ?? Encoding.UTF8,
                        param.ContentType ?? "application/json");
                }
            }

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(param.Timeout <= 0 ? 30000 : param.Timeout);
            response = await NetworkService.GetClient().SendAsync(request, cts.Token).ConfigureAwait(false);
            EnsureSuccess(response);
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        finally
        {
            if(!param.RequireContent) response?.Dispose();
            request.Dispose();
        }
    }

    public static HttpMethod ParseMethod(string? method)
    {
        return (method ?? "GET").ToUpperInvariant() switch
        {
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "HEAD" => HttpMethod.Head,
            _ => HttpMethod.Get
        };
    }

    public static bool SupportBody(HttpMethod method)
    {
        return method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch ||
               method == HttpMethod.Delete;
    }

    public static int Ping(string ip)
    {
        try
        {
            var result = new Ping().Send(ip);
            return result.Status == IPStatus.Success ? (int)result.RoundtripTime : -1;
        }
        catch
        {
            return -1;
        }
    }
}
