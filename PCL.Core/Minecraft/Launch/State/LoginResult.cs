namespace PCL.Core.Minecraft.Launch.State;
/*
/// <summary>
/// Minecraft 登录结果
/// </summary>
public class LoginResult {
    /// <summary>
    /// 玩家名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 玩家 UUID
    /// </summary>
    public string Uuid { get; set; }

    /// <summary>
    /// 访问令牌
    /// </summary>
    public string AccessToken { get; set; }

    /// <summary>
    /// 登录类型 ("Microsoft", "Auth", "Legacy")
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// 客户端令牌
    /// </summary>
    public string ClientToken { get; set; }

    /// <summary>
    /// 微软登录时返回的 profile 信息（JSON 格式）
    /// </summary>
    public string ProfileJson { get; set; }

    /// <summary>
    /// 登录是否成功
    /// </summary>
    public bool IsSuccess => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(Name);

    public LoginResult() {
        Name = string.Empty;
        Uuid = string.Empty;
        AccessToken = string.Empty;
        Type = string.Empty;
        ClientToken = string.Empty;
        ProfileJson = string.Empty;
    }

    public LoginResult(string name, string uuid, string accessToken, string type, string clientToken = "", string profileJson = "") {
        Name = name ?? string.Empty;
        Uuid = uuid ?? string.Empty;
        AccessToken = accessToken ?? string.Empty;
        Type = type ?? string.Empty;
        ClientToken = clientToken ?? string.Empty;
        ProfileJson = profileJson ?? string.Empty;
    }

    public override string ToString() {
        return $"McLoginResult[Name={Name}, Type={Type}, Success={IsSuccess}]";
    }
}
*/
