using System;
using System.Linq;
using System.Net.Http;

namespace PCL.Core.IO.Net.Http.Client.Request;

public static class HttpCookieExtension
{
    extension (HttpRequestMessage requestMessage)
    {
        public HttpRequestMessage WithCookie(string name, string value)
        {
            ArgumentNullException.ThrowIfNull(requestMessage);
            ArgumentNullException.ThrowIfNullOrEmpty(value);
            ArgumentNullException.ThrowIfNull(value);

            var newCookie = $"{name}={_GetSafeCookieValue(value)}";

            if (requestMessage.Headers.TryGetValues("Cookie", out var existingValues))
            {
                var existingCookie = string.Join("; ", existingValues);
                requestMessage.Headers.Remove("Cookie");
                requestMessage.Headers.Add("Cookie", $"{existingCookie}; {newCookie}");
            }
            else
            {
                requestMessage.Headers.Add("Cookie", newCookie);
            }

            return requestMessage;
        }
    }

    private static string _GetSafeCookieValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var needsEncoding = value.Any(c => _ForbiddenCookieValueChar.Contains(c) || char.IsControl(c));
        return needsEncoding ? Uri.EscapeDataString(value) : value;
    }

    private static readonly char[] _ForbiddenCookieValueChar = [';', ',', ' ', '\r', '\n', '\t', '\0', '=', '"', '\'', '\\', '<', '>'];

}
