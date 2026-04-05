using System.Text.Json.Serialization;
using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;

public record YggdrasilConnectMetaData: OpenIdMetadata
{
    [JsonPropertyName("shared_client_id")]
    public string? SharedClientId { get; init; }
}