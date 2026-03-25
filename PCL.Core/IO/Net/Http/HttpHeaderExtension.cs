using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

namespace PCL.Core.IO.Net.Http.Client.Request;

public static class HttpHeaderHandler
{
    extension (HttpRequestMessage requestMessage)
    {
        public HttpRequestMessage WithHeader(string key, string value)
        {
            if (key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase) && requestMessage.Content is not null)
                requestMessage.Content.Headers.TryAddWithoutValidation(key, value);
            else
                requestMessage.Headers.TryAddWithoutValidation(key, value);

            return requestMessage;
        }

        public HttpRequestMessage WithHeaders(IDictionary<string, string> pairs)
        {
            ArgumentNullException.ThrowIfNull(pairs);

            foreach (var item in pairs)
            {
                requestMessage.WithHeader(item.Key, item.Value);
            }

            return requestMessage;
        }

        public HttpRequestMessage WithHeader(KeyValuePair<string, string> pair) =>
            requestMessage.WithHeader(pair.Key, pair.Value);

        public HttpRequestMessage WithAuthentication(string scheme, string token)
        {
            ArgumentException.ThrowIfNullOrEmpty(scheme);
            ArgumentException.ThrowIfNullOrEmpty(token);

            requestMessage.Headers.Authorization = new AuthenticationHeaderValue(scheme, token);
            return requestMessage;
        }

        public HttpRequestMessage WithBearerToken(string token) => 
            requestMessage.WithAuthentication("Bearer", token);
    }
}
