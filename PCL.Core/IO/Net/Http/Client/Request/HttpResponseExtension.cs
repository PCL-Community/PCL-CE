using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Net.Http.Client.Request;

public static class HttpResponseExtension
{
    extension(HttpResponseMessage responseMessage)
    {
        public bool IsSuccess => responseMessage.IsSuccessStatusCode;

        public string AsString() { return responseMessage.AsStringAsync().GetAwaiter().GetResult(); }

        public async Task<string> AsStringAsync(CancellationToken ct = default)
        {
            try
            {
                return await responseMessage.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            } catch(TaskCanceledException)
            {
                throw new TimeoutException("The request was canceled due to a timeout.");
            } catch(OperationCanceledException)
            {
                throw new TimeoutException("The operation was canceled.");
            }
        }

        public async Task<Stream> AsStreamAsync(CancellationToken ct = default)
        {
            try
            {
                return await responseMessage.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            } catch(TaskCanceledException)
            {
                throw new TimeoutException("The request was canceled due to a timeout.");
            } catch(OperationCanceledException)
            {
                throw new TimeoutException("The operation was canceled.");
            }
        }

        public async Task<byte[]> AsByteArrayAsync(CancellationToken ct = default)
        {
            try
            {
                return await responseMessage.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            } catch(TaskCanceledException)
            {
                throw new TimeoutException("The request was canceled due to a timeout.");
            } catch(OperationCanceledException)
            {
                throw new TimeoutException("The operation was canceled.");
            }
        }

        public async Task<T?> AsJsonAsync<T>(
            JsonSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var stream = await responseMessage.AsStreamAsync(cancellationToken).ConfigureAwait(false);
                return await JsonSerializer.DeserializeAsync<T>(stream, options, cancellationToken)
                    .ConfigureAwait(false);
            } catch(JsonException ex)
            {
                throw new InvalidDataException("Failed to deserialize JSON response.", ex);
            }
        }

        public Dictionary<string, string[]> GetHeaders()
        {
            return responseMessage.Headers
                .Concat(responseMessage.Content.Headers)
                .ToDictionary(k => k.Key, v => v.Value.ToArray());
        }

        public Dictionary<string, string[]> GetContentHeaders()
        { return responseMessage.Content.Headers.ToDictionary(k => k.Key, v => v.Value.ToArray()); }

        public string[] GetHeader(string name)
        {
            if(responseMessage.Headers.TryGetValues(name, out var values) ||
                responseMessage.Content.Headers.TryGetValues(name, out values))
                return values.ToArray();

            return [];
        }

        public bool TryGetHeader(string name, out string[] values)
        {
            if(responseMessage.Headers.TryGetValues(name, out var headerValues) ||
                responseMessage.Content.Headers.TryGetValues(name, out headerValues))
            {
                values = headerValues.ToArray();
                return true;
            }

            values = [];
            return false;
        }

        public string? GetFirstHeaderValue(string name) { return responseMessage.GetHeader(name).FirstOrDefault(); }

        public bool TryGetFirstHeaderValue(string name, out string value)
        {
            var values = responseMessage.GetHeader(name);
            if(values.Length == 0)
            {
                value = string.Empty;
                return false;
            }
            value = values.First();
            return true;
        }

        public void EnsureSuccessStatusCode()
        {
            if(!responseMessage.IsSuccess)
                throw new HttpRequestException(
                    $"HTTP request failed with status code {responseMessage.StatusCode}: {responseMessage.ReasonPhrase}");
        }

        public async Task EnsureSuccessStatusCodeWithContentAsync(CancellationToken ct = default)
        {
            if(!responseMessage.IsSuccess)
            {
                var content = await responseMessage
                    .AsStringAsync(ct)
                    .ConfigureAwait(false);

                throw new HttpRequestException(
                    $"HTTP request failed with status code {responseMessage.StatusCode}: {responseMessage.ReasonPhrase}. Response content: {content}");
            }
        }
    }
}