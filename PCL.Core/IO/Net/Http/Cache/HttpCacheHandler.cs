using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net.Http.Cache.Models;

namespace PCL.Core.IO.Net.Http.Cache;

/// <summary>
/// HTTP 缓存处理器
/// </summary>
public class HttpCacheHandler:DelegatingHandler
{
    private HttpCacheRepository _repository;
    public HttpCacheHandler(HttpMessageHandler invoker, HttpCacheRepository repo)
    {
        InnerHandler = invoker;
        _repository = repo;
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return SendAsync(request, cancellationToken).GetAwaiter().GetResult();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if(!_repository.TryGetCacheData(request.RequestUri!.ToString(),out var details))
            return await base.SendAsync(request, cancellationToken);
        if (details.ExpiredAt is not null &&
            details.LastUpdate.AddSeconds((double)details.ExpiredAt) < DateTimeOffset.Now
            && _repository.TryGetCacheResponse(request,out var cacheResponse) && !details.EnsureValidate
           )
            return cacheResponse;
        
        if(details.Tag is not null) request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(details.Tag));
        if(details.LastModify is not null) request.Headers.IfModifiedSince = DateTimeOffset.Parse(details.LastModify);
        var response = base.Send(request, cancellationToken);
        if(response.StatusCode == HttpStatusCode.NotModified && _repository.TryGetCacheResponse(request,out cacheResponse))
            return cacheResponse;
        var handle = await _repository.TryBeginUpdateAsync(request.RequestUri.ToString());
        if (handle is not null)
            response.Content = new StreamContent(new CacheStream(handle,
                await response.Content.ReadAsStreamAsync(cancellationToken)));
        return response;
    }
}