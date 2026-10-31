# Minecraft/IdentityModel/Extensions/JsonWebToken 目录

JSON Web Token 支持，位于命名空间 `PCL.Core.Minecraft.IdentityModel.Extensions.JsonWebToken`。提供 JWT 解析、签名验证，以及 JWKS（JSON Web Key Set）模型的承载类。

## 本目录模块

- **JsonWebKeys.cs**：JSON Web 密钥集合模型（JWKS）。
- **JsonWebToken.cs**：JWT 解析/校验工具。

## 运行流程

本目录是「令牌校验」的底层工具，被 OpenID/Yggdrasil Connect 的 `id_token` 验证环节直接调用：

1. **获取签名密钥**：调用方（如 `YggdrasilConnectProvider._ReadIdTokenAsync`）先从 JWT 头部读出 `kid`，再通过 `OpenIdOptions.GetSignatureKeyAsync(kid, …)` 从 `jwks_uri` 拉取对应的 `JsonWebKey`。
2. **构造并验证**：用原始令牌字符串 + `OpenIdMetadata`（来自 `OpenIdOptions.Meta`）构造 `JsonWebToken`，调用 `VerifySignature(key, clientId)`。
3. **读取声明**：验证通过后调用 `ReadTokenPayload<T>()` 反序列化为声明模型（例如 `YggdrasilConnectClaims`）。

默认验证回调 `SecurityTokenValidateCallback` 使用 `JwtSecurityTokenHandler.ValidateToken` 校验：
- 发行者（`ValidateIssuer` / `ValidIssuer = meta.Issuer`）；
- 受众（`ValidateAudience`，仅当传入 `clientId` 非空时开启，`ValidAudience = clientId`）；
- 签名密钥（`ValidateIssuerSigningKey`，仅当传入 `key` 非空时开启）；
- 有效期（`ValidateLifetime`，`ClockSkew = 60s`），并额外检查 `iat`（签发时间）不能晚于「当前时间 + 时钟偏移」，否则抛出 `SecurityTokenInvalidLifetimeException`。

令牌的「生成」不在此目录，微软/第三方服务签发后下发，本目录只做解析与校验。

## 输入输出规范

### 关键类型与签名

- `JsonWebKeys`（record）：`[JsonPropertyName("keys")] required JsonWebKey[] Keys`。即 OpenID 发现文档 `jwks_uri` 返回的 `{ "keys": [...] }` 结构。
- `JsonWebToken` 构造：`JsonWebToken(string token, OpenIdMetadata meta)`。
  - `public delegate SecurityToken? TokenValidateCallback(OpenIdMetadata metadata, string token, JsonWebKey? key, string? clientId)` —— 验证策略可替换。
  - `public TokenValidateCallback SecurityTokenValidateCallback { get; set; }` —— 默认实现见上。
- 读取/校验方法：
  - `T? ReadTokenPayload<T>(bool allowUnverifyToken = false)`：反序列化 payload。`T` 可为 `Dictionary<string,object>`、`JwtPayload`，或任意带 `[JsonPropertyName]` 的模型类型（用 `JsonCompat.SerializerOptions` 序列化后再反序列化）。
  - `T? ReadTokenHeader<T>()`：读取头部，支持同上泛型。
  - `SecurityToken? VerifySignature(JsonWebKey key, string? clientId = null)` 与 `SecurityToken? VerifySignature()`：触发验证回调，成功则置 `_verified = true`。
  - `DateTime? GetExpirationTime()` / `DateTime? GetIssuedAtTime()`：`ValidTo`/`ValidFrom` 为 `DateTime.MinValue` 时返回 `null`。
  - `bool IsExpired()`：`UtcNow > exp` 返回 `true`；解析异常时视为已过期（返回 `true`）。
  - `string? GetClaimValue(string claimType, bool allowUnverifyToken = false)`：从 payload 取声明字符串，缺失返回 `null`。
  - `string GetTokenString()`：返回原始令牌。
  - `bool IsVerified { get; }`：是否已通过 `VerifySignature`。

### 序列化格式

- 令牌三段式 `header.payload.signature`，由 `System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler` 解析（`ReadJwtToken` / `CanReadToken`），内部为 base64url 编码的 JSON。
- 模型（payload/header）与 `JsonWebKeys` 的 JSON 映射依赖 `System.Text.Json` 的 `[JsonPropertyName]` 与 `JsonCompat.SerializerOptions`。

### 可能抛出的异常

- `SecurityException`：格式无效、未验证即读 payload（且未传 `allowUnverifyToken=true`）、签名验证失败、取过期/签发/声明值时底层异常被包装。
- `InvalidOperationException`：`ReadTokenPayload` 时 payload 为空、`ReadTokenHeader` 时 header 为空。
- `SecurityTokenInvalidLifetimeException`（被包装进 `SecurityException`）：`iat` 在未来，或有效期校验失败。

## 使用约束与注意事项

- **必须先验证再读声明**：`ReadTokenPayload`/`GetClaimValue` 默认要求 `IsVerified == true`，否则抛「不安全的令牌」。仅在令牌来源完全可信且不需要签名保证时，才传 `allowUnverifyToken: true`。`_verified` 为一次性状态，验证成功即永久置位。
- **签名密钥来源**：通过 `OpenIdOptions.GetSignatureKeyAsync` 从 `jwks_uri` 按 `kid` 拉取，不在本目录硬编码。任务说明提到的 `PCL.Core.Utils.Secret` 在本目录代码中**未直接使用**（Secret 主要用于加密存储/设备标识，见 `Utils/Secret`），密钥来源以 `JsonWebKey` 注入为准。
- **时钟偏移**：默认允许 60 秒 `ClockSkew`；`iat` 不能晚于「现在 + 偏移」，否则视为非法。部署环境时钟同步异常可能导致验证失败。
- **issuer / audience 校验**：`ValidIssuer` 取自 `OpenIdMetadata.Issuer`；`audience` 仅在传入 `clientId` 时校验，传入 `null`（无参 `VerifySignature()`）会跳过受众校验——若需严格校验务必传入 `clientId`。
- **算法限制**：`ValidAlgorithms` 取自 `meta.IdTokenSigningAlgValuesSupported`（为空则不限）。
- **线程安全**：实例字段 `_parsedToken`、`_verified` 等无锁保护，本类**不保证线程安全**，建议在单一线程/异步上下文中使用同一实例，或每次验证新建实例。
- **缓存行为**：`_ParseToken` 会缓存解析结果（惰性），重复读取不重复解析。
