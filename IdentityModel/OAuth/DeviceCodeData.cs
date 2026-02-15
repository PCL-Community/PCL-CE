using System.Text.Json.Serialization;
using PCL.Core.Utils.Exts;

namespace PCL.Core.IdentityModel.OAuth;

public record DeviceCodeData
{
    public bool IsError => !Error.IsNullOrEmpty();
    [JsonPropertyName("error")] public string? Error;

    [JsonPropertyName("error_description")]
    public string? ErrorDescription;

    [JsonPropertyName("user_code")] public string? UserCode;
    [JsonPropertyName("device_code")] public string? DeviceCode;
    [JsonPropertyName("verification_uri")] public string? VerificationUri;

    [JsonPropertyName("verification_uri_complete")]
    public string? VerificationUriComplete;

    [JsonPropertyName("interval")] public int? Interval;
    [JsonPropertyName("expired_in")] public int? ExpiredIn;
}