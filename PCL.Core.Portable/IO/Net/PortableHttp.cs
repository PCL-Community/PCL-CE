// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;

namespace PCL.Core.IO.Net;

/// <summary>
/// Minimal cross-platform HTTP entry point for portable services.
/// </summary>
public static class PortableHttp
{
    private static readonly Lazy<HttpClient> SharedClient = new(CreateClient);

    public static HttpClient Client => SharedClient.Value;

    public static Task<string> ReadStringAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            UseCookies = false,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 20,
            AutomaticDecompression = DecompressionMethods.All
        };
        return new HttpClient(handler, disposeHandler: true);
    }
}
