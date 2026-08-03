using System;
using System.Net.Http;

namespace PCL.Core.IO.Net.Http;

public class HttpResponseException:HttpRequestException,IDisposable
{
    public HttpResponseMessage? Response { get; init; }

    public HttpResponseException()
    {
        
    }

    public HttpResponseException(string message) : base(message)
    {
        
    }

    public HttpResponseException(string message, Exception? inner) : base(message, inner)
    {
        
    }

    public HttpResponseException(HttpResponseMessage? response) : this(
        $"{(int?)response?.StatusCode} {response?.ReasonPhrase ?? (response is null ? "undefined" : Enum.GetName(response.StatusCode))})")
    {
        Response = response;
    }

    public void Dispose()
    {
        Response?.Dispose();
        GC.SuppressFinalize(this);
    }

    ~HttpResponseException()
    {
        try
        {
            Response?.Dispose();
        }
        catch
        {
            // Suppress Exception
        }
    }
}