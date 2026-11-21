namespace PCL.Core.IO.Net.Http.Cache;

public record HttpCacheMetadata
{
    public string? ETag { get; init; }
    public string? LastModified { get; init; }
    public int StatusCode { get; init; }
}