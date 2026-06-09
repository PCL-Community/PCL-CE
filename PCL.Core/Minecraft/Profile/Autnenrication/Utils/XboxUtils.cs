using System.Threading.Tasks;
using PCL.Core.IO.Net.Http;
using PCL.Core.Minecraft.Profile.Autnenrication.Utils.Models;

namespace PCL.Core.Minecraft.Profile.Autnenrication.Utils;

public static class XboxUtils
{
    private const string XboxLiveAuthServer = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsAuthorizeServer = "https://xsts.auth.xboxlive.com/xsts/authorize";
    public static async Task<XboxLiveResponse?> AuthenticateAsync<T>(XboxAuthenticate<T> authData, bool isXsts)
    {
        using var response = await HttpRequest.CreatePost(
                isXsts ? XstsAuthorizeServer:XboxLiveAuthServer
                ).WithJsonContent(authData).SendAsync()
            .ConfigureAwait(false);
        
        return await response.AsJsonAsync<XboxLiveResponse>();
    }
}