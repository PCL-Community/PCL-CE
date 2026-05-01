using System.Text.Json.Serialization;
using PCL.CE.Core.Minecraft.IdentityModel.Extensions.OpenId;

namespace PCL.CE.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;

public record YggdrasilConnectMetaData: OpenIdMetadata
{
    [JsonPropertyName("shared_client_id")]
    public string? SharedClientId { get; init; }
}