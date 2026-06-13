// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net.Http.Headers;

namespace PCL.Core.IO.Download;

/// <summary>
/// HTTP 下载连接。响应正文按调用方提供的缓冲区读取，不为每个分块分配数组。
/// </summary>
public sealed class HttpDlConnection : IDlConnection, IDisposable, IAsyncDisposable
{
    private readonly HttpClient _client;
    private readonly string _url;
    private readonly Action<HttpRequestMessage>? _configureRequest;

    private HttpResponseMessage? _response;
    private Stream? _responseStream;
    private bool _started;
    private bool _stopped;

    public HttpDlConnection(
        HttpClient client,
        string url,
        Action<HttpRequestMessage>? configureRequest = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _url = url ?? throw new ArgumentNullException(nameof(url));
        _configureRequest = configureRequest;
    }

    public async ValueTask<NDlConnectionInfo> StartAsync(
        long beginOffset,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_stopped, this);
        if (_started)
            throw new InvalidOperationException("Connection has already been started.");
        _started = true;

        using var request = new HttpRequestMessage(HttpMethod.Get, _url);
        _configureRequest?.Invoke(request);

        if (beginOffset > 0)
            request.Headers.Range = new RangeHeaderValue(beginOffset, null);

        _response = await _client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        _response.EnsureSuccessStatusCode();

        _responseStream = await _response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        var length = _response.Content.Headers.ContentLength ?? -1;
        var endOffset = length >= 0 ? beginOffset + length - 1 : -1;
        var supportsSegments = _response.Headers.AcceptRanges.Contains("bytes");
        return new NDlConnectionInfo(length, beginOffset, endOffset, supportsSegments);
    }

    public ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (!_started)
            throw new InvalidOperationException("StartAsync must be called before ReadAsync.");
        ObjectDisposedException.ThrowIf(_stopped, this);
        return _responseStream is null
            ? ValueTask.FromResult(0)
            : _responseStream.ReadAsync(buffer, cancellationToken);
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_stopped)
            return ValueTask.CompletedTask;

        _stopped = true;
        _responseStream?.Dispose();
        _responseStream = null;
        _response?.Dispose();
        _response = null;
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _stopped = true;
        _responseStream?.Dispose();
        _response?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _stopped = true;
        if (_responseStream is IAsyncDisposable asyncStream)
            await asyncStream.DisposeAsync().ConfigureAwait(false);
        else
            _responseStream?.Dispose();
        _response?.Dispose();
    }
}
