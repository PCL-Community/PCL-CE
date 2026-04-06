using System;

namespace PCL.Core.IO.Net.Http.Cache.Models;

/// <summary>
/// HTTP 缓存信息
/// </summary>
public class HttpCacheDetails(HttpCacheRepository repo)
{
    public required DateTimeOffset LastUpdate { get; set; }
    public required string RequestUri { get; set; }
    public string? Tag { get; set; }
    public string? LastModify { get; set; }
    public int? ExpiredAt { get; set; }
    public bool EnsureValidate { get; set; }
    public string? Hash { get; set; }
    public HttpCacheStatus Status = HttpCacheStatus.Invalid;

    public HttpCacheUpdateHandle GetUpdateHandle()
    {
        return new HttpCacheUpdateHandle(repo)
        {
            Details = this
        };
    }
}