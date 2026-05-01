using System;
using System.Net.Http;

namespace PCL.CE.Core.IO.Net.Http.Client.Request;

public static class HttpBasicExtension
{
    extension(HttpRequestMessage requestMessage)
    {
        public HttpRequestMessage WithHttpVersionOption(Version httpVersion)
        {
            requestMessage.Version = httpVersion;
            return requestMessage;
        }
    }
}
