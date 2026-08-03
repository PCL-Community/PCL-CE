using PCL.Core.IO.Storage.Cache;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Net.Http.Cache;

/// <summary>
/// HTTP 缓存处理器
/// </summary>
public class HttpCacheHandler : DelegatingHandler
{
    private ICacheService _cacheService;
    public HttpCacheHandler(HttpMessageHandler invoker, ICacheService cacheService)
    {
        InnerHandler = invoker;
        _cacheService = cacheService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri?.ToString();
        if (string.IsNullOrEmpty(uri))
        {
            return await base.SendAsync(request, ct).ConfigureAwait(false);
        }

        // seek cache
        var cacheKey = CacheKeys.ApiResponse("http", uri);
        var cached = await _cacheService.GetAsync<byte[]>(cacheKey, ct).ConfigureAwait(false);
        if (cached.Found)
        {
            var cachedResponse = _DeserializeResponse(cached.Value!);
            cachedResponse.Headers.Add("X-Cache-Hit", "HIT");
            cachedResponse.RequestMessage = request;
            return cachedResponse;
        }

        // seek metadata
        var metaKey = CacheKeys.ApiResponseMeta("http", uri);
        var metaCached = await _cacheService.GetAsync<HttpCacheMetadata>(metaKey, ct).ConfigureAwait(false);
        if (metaCached.Found)
        {
            if (metaCached.Value!.ETag is not null)
            {
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(metaCached.Value!.ETag));
            }

            if (metaCached.Value!.LastModified is not null)
            {
                request.Headers.IfModifiedSince = DateTimeOffset.Parse(metaCached.Value!.LastModified);
            }
        }

        // send request
        var response = await base.SendAsync(request, ct).ConfigureAwait(false);
        if (response.Headers.CacheControl?.NoStore ?? false)
        {
            return response;
        }

        // not modified, return cached response if exists
        if (response.StatusCode is HttpStatusCode.NotModified)
        {
            var reCached = await _cacheService.GetAsync<byte[]>(cacheKey, ct).ConfigureAwait(false);
            if (reCached.Found)
            {
                var cachedResponse = _DeserializeResponse(reCached.Value!);
                cachedResponse.Headers.Add("X-Cache-Hit", "HIT");
                cachedResponse.RequestMessage = request;
                return cachedResponse;
            }
        }

        // cache response
        var body = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        var ttl = _ComputeTtl(response);

        await _cacheService.SetAsync(cacheKey, body, new CachePolicy
        {
            AbsoluteExpiration = ttl,
            Group = "http",
            Tags = "http-response"
        }, ct).ConfigureAwait(false);

        await _cacheService.SetAsync(metaKey, new HttpCacheMetadata
        {
            ETag = response.Headers.ETag?.Tag,
            LastModified = response.Content.Headers.LastModified?.ToString("O"),
            StatusCode = (int)response.StatusCode
        }, CachePolicy.NeverExpire, ct).ConfigureAwait(false);

        // not matched, return original response
        response.Headers.Add("X-Cache-Hit", "MISS");
        return response;
    }

    private static TimeSpan? _ComputeTtl(HttpResponseMessage response)
    {
        var cc = response.Headers.CacheControl;
        if (cc?.MaxAge is not null)
        {
            return cc.MaxAge;
        }

        if (cc?.SharedMaxAge is not null)
        {
            return cc.SharedMaxAge;
        }

        return TimeSpan.FromMinutes(5);
    }

    private static HttpResponseMessage _DeserializeResponse(byte[] data)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(data)
        };
    }
}
