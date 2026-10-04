using System;
using System.Net;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Infrastructure;

public sealed class CompApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string Provider { get; }
    public string? RawResponse { get; }

    public CompApiException(
        HttpStatusCode statusCode,
        string provider,
        string message,
        string? rawResponse = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Provider = provider;
        RawResponse = rawResponse;
    }

    public bool IsRateLimited => StatusCode == HttpStatusCode.TooManyRequests;
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
    public bool IsServerError => (int)StatusCode >= 500;
}
