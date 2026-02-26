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
    
    /// <summary>
    /// 访问令牌
    /// </summary>
    [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
    /// <summary>
    /// 刷新令牌
    /// </summary>
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }
    /// <summary>
    /// ID Token
    /// </summary>
    [JsonPropertyName("id_token")] public string? IdToken { get; init; }
    /// <summary>
    /// 令牌类型
    /// </summary>
    [JsonPropertyName("token_type")] public string? TokenType { get; init; }
    /// <summary>
    /// 过期时间
    /// </summary>
    [JsonPropertyName("expires_in")] public int? ExpiresIn { get; init; }
}