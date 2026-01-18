using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PCL.Core.Utils.Exts;

public static class HttpRequestExtension
{
    public static async Task<HttpRequestMessage> CloneAsync(this HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        if (request.Content != null)
        {
            clone.Content = await request.Content._DeepCloneAsync().ConfigureAwait(false);
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
        {
            clone.Options.TryAdd(option.Key, option.Value);
        }

        return clone;
    }

    private static async Task<HttpContent?> _DeepCloneAsync(this HttpContent content)
    {
        var ms = new MemoryStream();
        await content.CopyToAsync(ms).ConfigureAwait(false);
        ms.Position = 0;

        var clone = new StreamContent(ms);

        // 复制内容头（如 Content-Type, Content-Length 等）
        foreach (var header in content.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}