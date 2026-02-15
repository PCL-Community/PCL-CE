using System.Collections.Generic;

namespace PCL.Core.IdentityModel.OAuth;

public record OAuthClientOptions
{
    public Dictionary<string,string>? Headers {
        get;
        set;
    }
    public required EndpointMeta Meta { get; set; }
}