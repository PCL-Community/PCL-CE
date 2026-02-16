using System;
using System.Collections.Generic;
using System.Net.Http;

namespace PCL.Core.IdentityModel.OAuth;

public record OAuthClientOptions
{
    /// <summary>
    /// 
    /// </summary>
    public Dictionary<string,string>? Headers { get; set; }
    public required EndpointMeta Meta { get; set; }
    public required Func<HttpClient> GetClient { get; set; }

    public required string RedirectUri { get; set; }
    public required string ClientId { get; set; }
    
}