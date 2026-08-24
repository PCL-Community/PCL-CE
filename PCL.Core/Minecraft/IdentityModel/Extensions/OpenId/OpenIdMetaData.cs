using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;



public record OpenIdMetadata
{
    [JsonPropertyName("issuer")]
    public required string Issuer { get; init; }

    [JsonPropertyName("authorization_endpoint")]
    public string? AuthorizationEndpoint { get; init; }

    [JsonPropertyName("device_authorization_endpoint")]
    public string? DeviceAuthorizationEndpoint { get; init; }

    [JsonPropertyName("token_endpoint")]
    public required string TokenEndpoint { get; init; }

    [JsonPropertyName("userinfo_endpoint")]
    public required string UserInfoEndpoint { get; init; }

    [JsonPropertyName("registration_endpoint")]
    public string? RegistrationEndpoint { get; init; }

    [JsonPropertyName("jwks_uri")]
    public required string JwksUri { get; init; }

    [JsonPropertyName("scopes_supported")]
    public required IReadOnlyList<string> ScopesSupported { get; init; }

    [JsonPropertyName("subject_types_supported")]
    public required IReadOnlyList<string> SubjectTypesSupported { get; init; }

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public required IReadOnlyList<string> IdTokenSigningAlgValuesSupported { get; init; }

    
}