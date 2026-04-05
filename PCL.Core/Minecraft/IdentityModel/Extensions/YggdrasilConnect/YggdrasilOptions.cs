using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net.Http.Client.Request;
using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;

public record YggdrasilOptions:OpenIdOptions
{
    private string[] _scopesRequired = ["openid", "Yggdrasil.PlayerProfiles.Select", "Yggdrasil.Server.Join"];
    
    // 重写这个鬼方法是因为 Yggdrasil Connect 有要求（
    
    /// <summary>
    /// 拉取 Yggdrasil 配置
    /// </summary>
    /// <param name="token"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public override async Task InitializeAsync(CancellationToken token)
    {
        using var response = await HttpRequest
            .Create(OpenIdDiscoveryAddress)
            .WithHeaders(Headers ?? [])
            .SendAsync(GetClient.Invoke())
            .ConfigureAwait(false);

        Meta = (await response.AsJsonAsync<YggdrasilConnectMetaData>().ConfigureAwait(false))
            ?? throw new InvalidOperationException();
        if (_scopesRequired.Except(Meta.ScopesSupported).Any()) throw new InvalidOperationException();
    }
    /// <summary>
    /// 构建 OAuth 客户端选项
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">未调用 <see cref="InitializeAsync"/></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidCastException"></exception>
    public override async Task<OAuthClientOptions> BuildOAuthOptionsAsync(CancellationToken token)
    {
        if (Meta is YggdrasilConnectMetaData meta)
        {
            var options = await base.BuildOAuthOptionsAsync(token);
            if (!options.ClientId.IsNullOrEmpty()) return options;
            if (meta is null) throw new InvalidOperationException();
            if (!meta.SharedClientId.IsNullOrEmpty())
            {
                options.ClientId = meta.SharedClientId;
            }

            throw new ArgumentException();
        }

        throw new InvalidCastException();
    }
}