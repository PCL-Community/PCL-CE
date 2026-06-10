using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PCL.Core.IO.Download;

/// <summary>
/// HTTP-based download connection wrapping HttpClient.
/// </summary>
public class HttpDlConnection : IDlConnection, IDisposable
{
    private readonly HttpClient _client;
    private readonly string _url;
    private readonly Action<HttpRequestMessage>? _configureRequest;

    private HttpResponseMessage? _response;
    private Stream? _responseStream;
    private bool _started;
    private bool _stopped;

    public HttpDlConnection(HttpClient client, string url, Action<HttpRequestMessage>? configureRequest = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _url = url ?? throw new ArgumentNullException(nameof(url));
        _configureRequest = configureRequest;
    }

    public async Task<NDlConnectionInfo> StartAsync(long beginOffset)
    {
        if (_started)
            throw new InvalidOperationException("Connection has already been started.");
        _started = true;

        var request = new HttpRequestMessage(HttpMethod.Get, _url);
        _configureRequest?.Invoke(request);

        if (beginOffset > 0)
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(beginOffset, null);

        _response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
            .ConfigureAwait(false);
        _response.EnsureSuccessStatusCode();

        _responseStream = await _response.Content.ReadAsStreamAsync().ConfigureAwait(false);

        var length = _response.Content.Headers.ContentLength ?? -1;
        var endOffset = length >= 0 ? beginOffset + length - 1 : -1;
        var isSupportSegment = _response.Headers.AcceptRanges.Contains("bytes");

        return new NDlConnectionInfo(length, beginOffset, endOffset, isSupportSegment);
    }

    public async Task<byte[]> ReadAsync(int length)
    {
        if (!_started)
            throw new InvalidOperationException("StartAsync must be called before ReadAsync.");
        if (_stopped)
            throw new ObjectDisposedException(nameof(HttpDlConnection));
        if (_responseStream is null)
            return Array.Empty<byte>();

        var buffer = new byte[length];
        var totalRead = 0;
        while (totalRead < length)
        {
            var read = await _responseStream.ReadAsync(buffer, totalRead, length - totalRead)
                .ConfigureAwait(false);
            if (read == 0)
                break;
            totalRead += read;
        }

        if (totalRead == 0)
            return Array.Empty<byte>();

        if (totalRead == length)
            return buffer;

        var result = new byte[totalRead];
        Array.Copy(buffer, 0, result, 0, totalRead);
        return result;
    }

    public Task StopAsync()
    {
        if (_stopped)
            return Task.CompletedTask;
        _stopped = true;

        _responseStream?.Dispose();
        _responseStream = null;
        _response?.Dispose();
        _response = null;

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _responseStream?.Dispose();
        _response?.Dispose();
    }
}
