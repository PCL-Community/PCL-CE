using System;
using System.Net.Http;

namespace PCL.Core.Net.Http.Server;

public class HttpRoute(HttpMethod method, string path) : Attribute
{
    public HttpMethod Method { get; } = method;
    public string Path { get; } = path;
}