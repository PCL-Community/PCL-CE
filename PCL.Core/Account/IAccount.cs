using System.Security;

namespace PCL.Core.Account;

public interface IAccount
{
    /// <summary>
    /// 服务提供者名称。
    /// </summary>
    string ServiceName { get; }
    /// <summary>
    /// PCL CE 为账户生成的唯一标识符。
    /// </summary>
    string Uuid { get; }
    /// <summary>
    /// 账户显示名称。
    /// </summary>
    string Name { get; }
    /// <summary>
    /// 用于登录验证的账户用户名。
    /// </summary>
    string Username { get; }
    /// <summary>
    /// 用于登录验证的账户密码。应始终优先尝试使用 OAuth 验证。
    /// </summary>
    SecureString Password { get; }
    /// <summary>
    /// OAuth 服务地址。
    /// </summary>
    string OAuthAddress { get; }
    /// <summary>
    /// OAuth ClientId。
    /// </summary>
    string OAuthClientId { get; }
    /// <summary>
    /// OAuth AccessToken。
    /// </summary>
    SecureString AccessToken { get; }
    /// <summary>
    /// OAuth RefreshToken。
    /// </summary>
    SecureString RefreshToken { get; }
    /// <summary>
    /// 令牌到期时间。
    /// </summary>
    string ExpireTime { get; }
    /// <summary>
    /// 刷新 OAuth 令牌。
    /// </summary>
    /// <returns>是否刷新成功。</returns>
    bool OAuthRefresh();
}