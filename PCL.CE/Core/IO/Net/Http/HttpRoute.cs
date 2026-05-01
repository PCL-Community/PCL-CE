using System;
using System.Net.Http;

namespace PCL.CE.Core.IO.Net.Http;

public class HttpRoute(HttpMethod method, string path) : Attribute
{
    public HttpMethod Method { get; } = method;
    public string Path { get; } = path;
}