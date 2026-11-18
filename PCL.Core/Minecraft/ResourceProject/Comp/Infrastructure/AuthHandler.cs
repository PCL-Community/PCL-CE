using System.Net.Http;
using PCL.Core.IO.Net.Http;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Infrastructure;

public static class AuthHandler
{
    public static HttpRequestMessage ApplyCurseForgeAuth(this HttpRequestMessage request, string apiKey)
    {
        return request.WithHeader("x-api-key", apiKey);
    }

    public static HttpRequestMessage ApplyModrinthAuth(this HttpRequestMessage request, string? accessToken)
    {
        if (!string.IsNullOrEmpty(accessToken))
            return request.WithBearerToken(accessToken);
        return request;
    }
}
