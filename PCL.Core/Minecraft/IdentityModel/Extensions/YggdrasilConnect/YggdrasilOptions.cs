using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Utils.Exts;
using PCL.Core.IO.Net.Http;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;

public record YggdrasilOptions:OpenIdOptions
{
    private string[] _scopesRequired = ["openid", "Yggdrasil.PlayerProfiles.Select", "Yggdrasil.Server.Join"];

    // 重写这个鬼方法是因为 Yggdrasil Connect 有要求（

    /// <summary>
    /// 拉取 Yggdrasil 配置
    /// </summary>
    /// <param name="token"></param>
    /// <exception cref="IdentityModelConfigurationException">无法加载元数据或缺少必要 scope</exception>
    public override async Task InitializeAsync(CancellationToken token)
    {
        using var response = await HttpRequest
            .Create(OpenIdDiscoveryAddress)
            .WithHeaders(Headers ?? [])
            .SendAsync(GetClient.Invoke(), cancellationToken: token)
            .ConfigureAwait(false);

        Meta = (await response.AsJsonAsync<YggdrasilConnectMetaData>(cancellationToken: token).ConfigureAwait(false))
            ?? throw new IdentityModelConfigurationException("无法加载 Yggdrasil Connect 元数据");

        var missingScopes = _scopesRequired.Except(Meta.ScopesSupported).ToArray();
        if (missingScopes.Length > 0)
            throw new IdentityModelConfigurationException($"Yggdrasil Connect 元数据缺少必要 scope：{string.Join(", ", missingScopes)}");
    }
    /// <summary>
    /// 构建 OAuth 客户端选项
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="IdentityModelConfigurationException">未调用 <see cref="InitializeAsync"/> 或缺少必要的客户端配置</exception>
    public override async Task<OAuthClientOptions> BuildOAuthOptionsAsync(CancellationToken token)
    {
        if (Meta is YggdrasilConnectMetaData meta)
        {
            var options = await base.BuildOAuthOptionsAsync(token);
            if (!options.ClientId.IsNullOrEmpty()) return options;
            if (!meta.SharedClientId.IsNullOrEmpty())
            {
                options.ClientId = meta.SharedClientId;
                return options;
            }

            throw new IdentityModelConfigurationException("Yggdrasil Connect 需要设置 ClientId，或由元数据提供 sharedClientId");
        }

        throw new IdentityModelConfigurationException("请先调用 InitializeAsync() 加载 Yggdrasil Connect 元数据");
    }
}
