using System.Text.Json.Serialization;
using PCL.Core.Utils.Exts;

namespace PCL.Core.IdentityModel.OAuth;

public record AuthorizeResult
{
    public bool IsSecuess => Error.IsNullOrEmpty();
    /// <summary>
    /// 错误类型 (e.g. invalid_request)
    /// </summary>
    [JsonPropertyName("error")] public string? Error;
    /// <summary>
    /// 描述此错误的文本
    /// </summary>
    [JsonPropertyName("error_descripton")] public string? ErrorDescription;
    
    // 不用 SecureString，因为这东西依赖 DPAPI，不是最佳实践
    
    [JsonPropertyName("access_token")] public string? AccessToken;
    [JsonPropertyName("refresh_token")] public string? RefreshToken;
    [JsonPropertyName("token_type")] public string? TokenType;
    [JsonPropertyName("expires_in")] public int? ExpiresIn;
}