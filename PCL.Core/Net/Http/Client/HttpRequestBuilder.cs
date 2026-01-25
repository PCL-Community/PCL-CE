using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Net.Http.Client;

public class HttpRequestBuilder
{
    private readonly HttpRequestMessage _request;
    private readonly Dictionary<string, string> _cookies = [];
    private HttpCompletionOption _completionOption = HttpCompletionOption.ResponseContentRead;
    private bool _addLauncherHeader = true;
    private bool _doLog = true;
    private Version _requestVersion = HttpVersion.Version20;
    private TimeSpan _timeOutMillisec = TimeSpan.FromMilliseconds(30 * 1000);
    private bool _isEndOfLife = false;

    private HttpRequestBuilder(Uri uri, HttpMethod? method = null)
    {
        _request = new HttpRequestMessage(method ?? HttpMethod.Get, uri);
    }

    /// <summary>
    /// 创建一个 HttpRequestBuilder 对象
    /// </summary>
    public static HttpRequestBuilder Create(string url, HttpMethod? method = null)
    {
        return new HttpRequestBuilder(new Uri(url), method);
    }

    /// <summary>
    /// 创建一个 HttpRequestBuilder 对象
    /// </summary>
    public static HttpRequestBuilder Create(Uri uri, HttpMethod? method = null)
    {
        return new HttpRequestBuilder(uri, method);
    }

    /// <summary>
    /// 设置请求载荷
    /// </summary>
    public HttpRequestBuilder WithContent(HttpContent content, string? contentType = null)
    {
        _request.Content = content;
        if (contentType is not null) WithHeader("Content-Type", contentType);
        return this;
    }

    public HttpRequestBuilder WithContent(string content, string? contentType = null)
    {
        _request.Content = contentType is null
            ? new StringContent(content, Encoding.UTF8)
            : new StringContent(content, Encoding.UTF8, contentType);
        return this;
    }

    public HttpRequestBuilder WithJsonContent(dynamic content)
    {
        _request.Content = new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json");
        return this;
    }

    /// <summary>
    /// 设置一个请求所用的 Cookie，如果已设置过对应的键，旧的则会被覆盖
    /// </summary>
    public HttpRequestBuilder WithCookie(string key, string value)
    {
        _cookies[key] = value;
        return this;
    }

    /// <summary>
    /// 设置多个请求所用的 Cookie，如果已设置过对应的键，旧的则会被覆盖
    /// </summary>
    public HttpRequestBuilder WithCookie(IDictionary<string, string> cookies)
    {
        foreach (var cookie in cookies) _cookies[cookie.Key] = cookie.Value;
        return this;
    }

    /// <summary>
    /// 设置多个 Header
    /// </summary>
    public HttpRequestBuilder WithHeader(IDictionary<string, string> headers)
    {
        foreach (var header in headers) WithHeader(header.Key, header.Value);
        return this;
    }

    /// <summary>
    /// 设置一个 Header
    /// </summary>
    public HttpRequestBuilder WithHeader(string key, string value)
    {
        if (key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase) && _request.Content is not null)
            _request.Content.Headers.TryAddWithoutValidation(key, value);
        else
            _request.Headers.TryAddWithoutValidation(key, value);
        return this;
    }

    public HttpRequestBuilder WithHeader(KeyValuePair<string, string> header) => WithHeader(header.Key, header.Value);

    public HttpRequestBuilder WithAuthentication(string scheme, string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheme);
        ArgumentException.ThrowIfNullOrEmpty(token);
        _request.Headers.Authorization = new AuthenticationHeaderValue(scheme, token);
        return this;
    }

    public HttpRequestBuilder WithAuthentication(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        _request.Headers.Authorization = new AuthenticationHeaderValue(token);
        return this;
    }

    public HttpRequestBuilder WithBearerToken(string token) => WithAuthentication("Bearer", token);

    public HttpRequestBuilder WithDefaultHeaderOption(bool hasDefaultHeader = true)
    {
        _addLauncherHeader = hasDefaultHeader;
        return this;
    }

    public HttpRequestBuilder WithHttpVersionOption(Version httpVersion)
    {
        _requestVersion = httpVersion;
        return this;
    }

    public HttpRequestBuilder WithLoggingOptions(bool doLog)
    {
        _doLog = doLog;
        return this;
    }

    public HttpRequestBuilder WithCompletionOption(HttpCompletionOption option)
    {
        _completionOption = option;
        return this;
    }

    public HttpRequestBuilder WithTimeOut(uint millisec)
    {
        _timeOutMillisec = TimeSpan.FromMilliseconds(millisec);
        return this;
    }

    public HttpRequestBuilder WithTimeOut(TimeSpan timeSpan)
    {
        _timeOutMillisec = timeSpan;
        return this;
    }

    /// <summary>
    /// 发起请求
    /// </summary>
    /// <param name="throwIfNotSuccess">请求失败时是否抛出异常</param>
    /// <param name="retryTimes">请求重试次数</param>
    /// <param name="ct">取消令牌</param>
    /// <param name="retryPolicy">依据请求当前尝试的次数给出的重试时长控制方法</param>
    public async Task<HttpResponseHandler> SendAsync(
        bool throwIfNotSuccess = false,
        int retryTimes = 3,
        CancellationToken ct = default,
        Func<int, TimeSpan>? retryPolicy = null)
    {
        if (_isEndOfLife) throw new ObjectDisposedException(nameof(HttpRequestBuilder));

        _PrepareRequestParameters();

        var client = NetworkService.GetClient();
        _request.Version = _requestVersion;
        _request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeOutMillisec);

        _MakeLog($"向 {_request.RequestUri} 发起 {_request.Method} 请求");

        try
        {
            var responseMessage = await NetworkService.GetRetryPolicy(retryTimes, retryPolicy)
                .ExecuteAsync(async token =>
                {
                    using var requestCopy = await _request.CloneAsync();
                    return await client.SendAsync(requestCopy, _completionOption, token).ConfigureAwait(false);
                }, cts.Token)
                .ConfigureAwait(false);

            var responseUri = responseMessage.RequestMessage?.RequestUri;
            if (responseUri != null && _request.RequestUri != responseUri)
                _MakeLog($"已重定向至 {responseUri}");

            _MakeLog($"已获取请求结果，返回 HTTP 状态码: {responseMessage.StatusCode}");

            if (throwIfNotSuccess) responseMessage.EnsureSuccessStatusCode();

            return new HttpResponseHandler(responseMessage);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"网络请求超时 (>{_timeOutMillisec.TotalMilliseconds}ms): {_request.RequestUri}");
        }
        finally
        {
            _request.Dispose();
            _isEndOfLife = true;
        }
    }

    private void _PrepareRequestParameters()
    {
        if (_cookies.Count != 0)
        {
            _request.Headers.Remove("Cookie");
            var cookiesCtx = new StringBuilder(_cookies.Count * 40);
            foreach (var cookie in _cookies)
            {
                if (cookiesCtx.Length > 0) cookiesCtx.Append("; ");
                cookiesCtx.Append(Uri.EscapeDataString(cookie.Key)).Append('=').Append(_GetSafeCookieValue(cookie.Value));
            }
            _request.Headers.TryAddWithoutValidation("Cookie", cookiesCtx.ToString());
        }

        if (_addLauncherHeader)
        {
            _request.Headers.TryAddWithoutValidation("User-Agent", $"PCL-Community/PCL2-CE/{Basics.VersionName} (pclc.cc)");
            _request.Headers.TryAddWithoutValidation("Referer", $"https://{Basics.VersionCode}.ce.open.pcl2.server/");
        }
    }

    private void _MakeLog(string msg)
    {
        if (_doLog) LogWrapper.Info("Network", msg);
    }

    private static string _GetSafeCookieValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var needsEncoding = value.Any(c => _ForbiddenCookieValueChar.Contains(c) || char.IsControl(c));
        return needsEncoding ? Uri.EscapeDataString(value) : value;
    }

    private static readonly char[] _ForbiddenCookieValueChar = [';', ',', ' ', '\r', '\n', '\t', '\0', '=', '"', '\'', '\\', '<', '>'];
}