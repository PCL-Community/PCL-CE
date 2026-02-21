using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;

public record YggdrasilOptions:OpenIdOptions
{
    private string[] _scopesRequired = ["openid", "Yggdrasil.PlayerProfiles.Select", "Yggdrasil.Server.Join"];
    public YggdrasilOptions(Func<HttpClient> getClient, string configurationAddress):base(getClient,configurationAddress)
    {

    }
    public override async Task InitiateAsync(CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, OpenIdDiscoveryAddress);
        if (Headers is not null)
            foreach (var kvp in Headers)
            {
                _ = request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
            }

        using var response = await GetClient.Invoke().SendAsync(request, token);
        Meta = JsonSerializer.Deserialize<YggdrasilConnectMetaData>(await response.Content.ReadAsStringAsync(token));
        if (Meta is null) throw new InvalidOperationException();
        if (_scopesRequired.Except(Meta.ScopesSupported).Any()) throw new InvalidOperationException();
    }

    public override async Task<OAuthClientOptions> BuildOAuthOptionsAsync(CancellationToken token)
    {
        await InitiateAsync(token);
        if (Meta is YggdrasilConnectMetaData meta)
        {
            var options = await base.BuildOAuthOptionsAsync(token);
            if (!options.ClientId.IsNullOrEmpty()) return options;
            if (meta is null) throw new InvalidOperationException();
            if (!meta.SharedClientId.IsNullOrEmpty())
            {
                options.ClientId = meta.SharedClientId;
            }

            throw new ArgumentException("ClientId");
        }

        throw new InvalidCastException($"Can not cast {Meta?.GetType().FullName} to YggdrasilConnectMetaData");
    }
}