using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using PCL.Core.Utils;

namespace PCL.Core.Net.Http.Server;

/// <summary>
/// 用于 <see cref="HttpServer"/> 响应客户端请求的服务端响应结构。
/// </summary>
[Serializable]
public class HttpRouteResponse
{
    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public HttpStatusCode? StatusCode = null;

    /// <summary>
    /// 此响应 <see cref="InputStream"/> 使用的字符编码
    /// </summary>
    public Encoding? ContentEncoding = null;

    /// <summary>
    /// [Header] 内容 MIME 类型
    /// </summary>
    public string? ContentType = null;

    /// <summary>
    /// [Header] 重定向目标 URL 或路径。
    /// </summary>
    public string? RedirectLocation = null;

    /// <summary>
    /// [Header] 是否使用分块传输编码。
    /// </summary>
    public bool? SendChunked = null;

    /// <summary>
    /// 随响应添加的 Cookies。
    /// </summary>
    public CookieCollection? Cookies = null;

    /// <summary>
    /// 用于传输响应内容的输入流，若非空值，该流将被直接 <c>CopyTo</c> 到实际响应的 <c>OutputStream</c> 中。
    /// </summary>
    public Stream? InputStream = null;

    /// <summary>
    /// 向标准 <see cref="HttpListener"/> 的响应对象写入数据。
    /// </summary>
    /// <param name="target">目标对象</param>
    public void Pour(HttpListenerResponse target)
    {
        target.StatusCode = (int)(StatusCode ?? HttpStatusCode.OK);
        if (ContentType is {} contentType) target.ContentType = contentType;
        if (ContentEncoding is {} contentEncoding) target.ContentEncoding = contentEncoding;
        if (RedirectLocation is {} redirectLocation) target.RedirectLocation = redirectLocation;
        if (SendChunked is {} sendChunked) target.SendChunked = sendChunked;
        if (Cookies is {} cookies) target.Cookies = cookies;
        if (InputStream is {} inputStream) inputStream.CopyTo(target.OutputStream);
    }

    /// <summary>
    /// 返回指定 HTTP 状态码的空响应
    /// </summary>
    /// <param name="statusCode">HTTP 状态码</param>
    public static HttpRouteResponse Empty(HttpStatusCode statusCode) => new() { StatusCode = statusCode };

    /// <summary>
    /// 默认的 204 (No Content) 响应。
    /// </summary>
    public static readonly HttpRouteResponse NoContent = Empty(HttpStatusCode.NoContent);

    /// <summary>
    /// 默认的 400 (Bad Request) 响应。
    /// </summary>
    public static readonly HttpRouteResponse BadRequest = Empty(HttpStatusCode.BadRequest);

    /// <summary>
    /// 默认的 403 (Forbidden) 响应。
    /// </summary>
    public static readonly HttpRouteResponse Forbidden = Empty(HttpStatusCode.Forbidden);

    /// <summary>
    /// 默认的 404 (Not Found) 响应。
    /// </summary>
    public static readonly HttpRouteResponse NotFound = Empty(HttpStatusCode.NotFound);

    /// <summary>
    /// 默认的 500 (Internal Server Error) 响应。
    /// </summary>
    public static readonly HttpRouteResponse InternalServerError = Empty(HttpStatusCode.InternalServerError);

    /// <summary>
    /// 默认的 502 (Bad Gateway) 响应。
    /// </summary>
    public static readonly HttpRouteResponse BadGateway = Empty(HttpStatusCode.BadGateway);

    /// <summary>
    /// 响应指定输入流的内容。
    /// </summary>
    /// <param name="stream">输入流</param>
    /// <param name="contentType">内容 MIME 类型</param>
    /// <param name="encoding">输入流内容使用的字符编码，默认为 UTF-8</param>
    public static HttpRouteResponse Input(Stream stream, string contentType = "application/octet-stream", Encoding? encoding = null) =>
        new() { InputStream = stream, ContentType = contentType, ContentEncoding = encoding ?? Encoding.UTF8 };

    /// <summary>
    /// 响应指定文本内容。
    /// </summary>
    /// <param name="text">文本内容</param>
    /// <param name="contentType">内容 MIME 类型</param>
    /// <param name="encoding">响应流使用的字符编码，默认为 UTF-8</param>
    public static HttpRouteResponse Text(string text, string contentType = "text/plain", Encoding? encoding = null) =>
        Input(new StringStream(text, encoding), contentType, encoding);

    /// <summary>
    /// 响应重定向。
    /// </summary>
    /// <param name="location">重定向目标 URL 或路径</param>
    /// <param name="statusCode">重定向状态码</param>
    public static HttpRouteResponse Redirect(string location, HttpStatusCode statusCode = HttpStatusCode.Found) =>
        new() { StatusCode = statusCode, RedirectLocation = location };

    /// <summary>
    /// 响应指定对象序列化得到的 JSON 内容，固定使用 UTF-8 编码
    /// </summary>
    /// <param name="obj">用于序列化的对象</param>
    /// <param name="options">JSON 序列化选项</param>
    public static HttpRouteResponse Json(object obj, JsonSerializerOptions? options)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, obj, options);
        stream.Position = 0;
        return new HttpRouteResponse
        {
            ContentEncoding = Encoding.UTF8,
            ContentType = "application/json, charset=utf-8",
            InputStream = stream
        };
    }

    /// <summary>
    /// 响应指定对象序列化得到的 JSON 内容，固定使用 UTF-8 编码
    /// </summary>
    /// <param name="obj">用于序列化的对象</param>
    public static HttpRouteResponse Json(object obj) => Json(obj, null);
}
