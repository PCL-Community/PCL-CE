using System.Text.Json.Serialization;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Minecraft.IdentityModel.OAuth;

public record AuthorizeResult
{
    public bool IsError => !Error.IsNullOrEmpty();
    /// <summary>
    /// 错误类型 (e.g. invalid_request)
    /// </summary>
    [JsonPropertyName("error")] public string? Error { get; init; }
    /// <summary>
    /// 描述此错误的文本
    /// </summary>
    [JsonPropertyName("error_description")] public string? ErrorDescription { get; init; }
    
    // 不用 SecureString，因为这东西依赖 DPAPI，不是最佳实践
    
    [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }
    [JsonPropertyName("id_token")] public string? IdToken { get; init; }
    [JsonPropertyName("token_type")] public string? TokenType { get; init; }
    [JsonPropertyName("expires_in")] public int? ExpiresIn { get; init; }
}