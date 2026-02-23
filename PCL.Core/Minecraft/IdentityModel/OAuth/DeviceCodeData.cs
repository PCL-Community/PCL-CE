using System.Text.Json.Serialization;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Minecraft.IdentityModel.OAuth;

public record DeviceCodeData
{
    public bool IsError => !Error.IsNullOrEmpty();
    /// <summary>
    /// 错误类型
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
    /// <summary>
    /// 错误描述
    /// </summary>
    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }
    /// <summary>
    /// 用户授权码
    /// </summary>
    [JsonPropertyName("user_code")]
    public string? UserCode { get; init; }
    /// <summary>
    /// 设备授权码
    /// </summary>
    [JsonPropertyName("device_code")]
    public string? DeviceCode { get; init; }
    /// <summary>
    /// 验证 Uri
    /// </summary>
    [JsonPropertyName("verification_uri")]
    public string? VerificationUri { get; init; }
    /// <summary>
    /// 验证 Uri （自动填充代码）
    /// </summary>
    [JsonPropertyName("verification_uri_complete")]
    public string? VerificationUriComplete { get; init; }
    /// <summary>
    /// 轮询间隔
    /// </summary>
    [JsonPropertyName("interval")]
    public int? Interval { get; init; }
    /// <summary>
    /// 过期时间
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; init; }
}