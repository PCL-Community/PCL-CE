# Minecraft/IdentityModel/Extensions/Pkce 目录

PKCE 支持，位于命名空间 `PCL.Core.Minecraft.IdentityModel.Extensions.Pkce`。为 OAuth 公共客户端提供 Proof Key for Code Exchange 质询（RFC 7636），在授权码流中防范授权码拦截攻击。本身是对 `SimpleOAuthClient` 的装饰（decorator）。

## 本目录模块

- **PkceChallengeOptions.cs**：PKCE 质询选项（method、verifier、challenge）。
- **PkceClient.cs**：PKCE 客户端（生成质询并参与授权流程）。

## 运行流程

`PkceClient` 实现 `IOAuthClient`，内部持有 `SimpleOAuthClient _client`，仅在「授权码流」相关步骤插入 PKCE 参数，其余方法直接转发：

1. **生成质询（授权阶段）**：`GetAuthorizeUrl(scopes, state, extData)` 执行：
   - `RandomNumberGenerator.Fill(_ChallengeCode)`（32 字节随机 `code_verifier` 原始字节）。
   - 计算 `code_challenge`：`Sha256` → `SHA256Provider.Instance.ComputeHash(...).ToHexString()`；`PlainText` → `_ChallengeCode.FromBytesToB64UrlSafe()`。
   - 附加 `code_challenge` 与 `code_challenge_method`（`"S256"` 或 `"plain"`）到 `extData`，置 `_isCallGetAuthorizeUrl = true`，再委托底层生成授权 URL。
2. **兑换令牌（令牌阶段）**：`AuthorizeWithCodeAsync(code, …)` 要求 `_isCallGetAuthorizeUrl == true`（否则抛 `InvalidOperationException("Challenge code is invalid")`），把 `_ChallengeCode.FromBytesToB64UrlSafe()` 作为 `code_verifier` 注入 `extData`，重置标志后委托兑换。授权服务器据此校验 verifier 与挑战匹配。
3. **设备流 / 刷新流 / 静默刷新**：`GetCodePairAsync` / `AuthorizeWithDeviceAsync` / `AuthorizeWithSilentAsync` 直接转发，不附加任何 PKCE 参数（PKCE 仅用于授权码流）。

典型装配：`OpenIdClient` 在 `EnablePkceSupport == true` 时用本类包裹 `OAuthClientOptions`。

## 输入输出规范

### PkceChallengeOptions（enum）
- `Sha256`：挑战方法 S256（推荐，默认）。
- `PlainText`：明文挑战方法 plain（安全性较弱，仅兼容旧服务）。

### PkceClient（class : IOAuthClient）
- 构造：`PkceClient(OAuthClientOptions options)`。
- 属性：`PkceChallengeOptions ChallengeMethod { get; private set; } = PkceChallengeOptions.Sha256`（默认 S256，类中未提供公开 setter，按默认 S256 行为）。
- 方法签名（与 `IOAuthClient` 一致）：
  - `string GetAuthorizeUrl(string[] scopes, string state, Dictionary<string,string>? extData)`
  - `Task<AuthorizeResult?> AuthorizeWithCodeAsync(string code, CancellationToken token, Dictionary<string,string>? extData = null)` —— 未先调用 `GetAuthorizeUrl` 抛 `InvalidOperationException`。
  - `Task<DeviceCodeData?> GetCodePairAsync(string[] scopes, CancellationToken token, Dictionary<string,string>? extData = null)`
  - `Task<AuthorizeResult?> AuthorizeWithDeviceAsync(DeviceCodeData data, CancellationToken token, Dictionary<string,string>? extData = null)`
  - `Task<AuthorizeResult?> AuthorizeWithSilentAsync(AuthorizeResult data, CancellationToken token, Dictionary<string,string>? extData = null)`

### 注入的扩展字段（RFC 6749 之外的 PKCE 字段）
- 授权 URL：`code_challenge`（十六进制 SHA256 哈希或 base64url 明文）、`code_challenge_method`（`"S256"`/`"plain"`）。
- 令牌请求：`code_verifier`（32 字节随机值的 base64url 安全编码）。

## 使用约束与注意事项

- **非线程安全**：类注释明确声明「此客户端并非线程安全，请勿在多个线程间共享实例」。原因：`_ChallengeCode`、`_isCallGetAuthorizeUrl` 是实例可变状态，并发调用 `GetAuthorizeUrl`/`AuthorizeWithCodeAsync` 会错乱 verifier 与标志。每个授权会话应使用独立 `PkceClient` 实例。
- **调用顺序强约束**：必须先 `GetAuthorizeUrl` 再 `AuthorizeWithCodeAsync`；后者会校验 `_isCallGetAuthorizeUrl` 并在成功后重置，重复兑换或顺序颠倒都会抛 `InvalidOperationException`。`code_verifier` 必须由同一次 `GetAuthorizeUrl` 生成的挑战派生，跨会话混用会导致服务器拒绝。
- **算法依赖**：哈希用 `PCL.Core.Utils.Hash.SHA256Provider`，base64url 编码用 `PCL.Core.Utils.Exts` 的 `FromBytesToB64UrlSafe()`。注意 S256 模式输出为**十六进制字符串**（`ToHexString`），而非 base64url 的哈希本身——这与某些库「S256 = base64url(SHA256)」的约定不同，对接外部服务时需确认期望格式。
- **仅作用于授权码流**：设备流/刷新流不携带 PKCE 参数，无需调用 `GetAuthorizeUrl`。
- **未直接使用密钥/机密**：本目录不涉及 `PCL.Core.Utils.Secret` 或客户端密钥，`code_verifier` 为临时随机值，不持久化。
