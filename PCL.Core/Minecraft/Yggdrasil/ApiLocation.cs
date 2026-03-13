using System;
using System.Threading.Tasks;
using PCL.Core.IO.Net.Http.Client.Request;

namespace PCL.Core.Minecraft.Yggdrasil;

public static class ApiLocation
{
    public static async Task<string> TryRequestAsync(string address)
    {
        var originAddr = address.StartsWith("http") ? address : $"https://{address}";
        var originUri = new Uri(originAddr);
        using var response = await HttpRequest
            .CreateHead(originAddr)
            .SendAsync();

        
        if (!response.TryGetHeader("X-Authlib-Injector-Api-Location", out var location)
            || location.Length == 0) 
            return originAddr;

        // TODO: use schema instead
        var resultAddr = location[0];

        if (string.IsNullOrEmpty(resultAddr)) return originAddr;
        if (resultAddr.StartsWith(originUri.Scheme)) return resultAddr;
        // 不允许 HTTPS 降 HTTP
        if (resultAddr.StartsWith("http:") && originUri.Scheme == "https") 
            return resultAddr.Replace("http","https");
        
        return new Uri(originUri, resultAddr).ToString();   
    }
}

