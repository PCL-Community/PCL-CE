// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net.Http.Headers;
using PCL.Platform.Abstractions.Java;

namespace PCL.Application.Minecraft.Java;

public sealed class HttpJavaRuntimeMetadataProvider : IJavaRuntimeMetadataProvider, IDisposable
{
    private const string RuntimeIndexUrl =
        "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public HttpJavaRuntimeMetadataProvider()
        : this(CreateDefaultClient(), ownsClient: true)
    {
    }

    public HttpJavaRuntimeMetadataProvider(HttpClient client, bool ownsClient = false)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = ownsClient;
    }

    public async ValueTask<string> GetRuntimeIndexAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _client.GetAsync(RuntimeIndexUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<string> GetManifestAsync(string manifestUrl, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestUrl);
        using HttpResponseMessage response = await _client.GetAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }

    private static HttpClient CreateDefaultClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-N/1.0");
        return client;
    }
}
