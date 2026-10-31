# Minecraft/IdentityModel/Extensions/OpenId 目录

OpenID Connect 支持，位于命名空间 `PCL.Core.Minecraft.IdentityModel.Extensions.OpenId`。封装 OIDC 发现文档拉取、JWKS 获取，以及授权码流/设备代码流的客户端外观（facade）。

## 本目录模块

- **OpenIdClient.cs**：OpenID Connect 客户端（执行授权码/令牌交换流程）。
- **OpenIdMetaData.cs**：OpenID 元数据模型（发现文档）。
- **OpenIdOptions.cs**：OpenID 选项配置。

## 运行流程

`OpenIdClient` 是 OIDC 流程的「总入口」外观，本身不直接发协议请求，而是根据配置装配底层 `IOAuthClient`：

1. **初始化**：`OpenIdClient.InitializeAsync(token, checkAddress = false)` →
   - `options.InitializeAsync(token)`：从 `OpenIdDiscoveryAddress` 拉取发现文档并填充 `Meta`（`OpenIdMetadata`）。
   - `options.BuildOAuthOptions()`：把元数据与配置转成 `OAuthClientOptions`。
   - 若 `checkAddress` 为真且元数据既无 `AuthorizationEndpoint` 也无 `DeviceAuthorizationEndpoint`，抛 `IdentityModelConfigurationException`。
   - 根据 `options.EnablePkceSupport` 选择底层实现：`true` → `PkceClient(opt)`（授权码流带 PKCE），`false` → `SimpleOAuthClient(opt)`。
2. **发起授权 / 兑换令牌**：`GetAuthorizeUrl` / `AuthorizeWithCodeAsync` / `GetCodePairAsync` / `AuthorizeWithDeviceAsync` / `AuthorizeWithSilentAsync` 全部委托给装配好的底层客户端，统一接口为 `IOAuthClient`。任何未先 `InitializeAsync` 的调用都会抛 `IdentityModelConfigurationException`。

`OpenIdOptions` 还承担「密钥获取」职责：`GetSignatureKeyAsync(kid, token)` 用 `Meta.JwksUri` 拉取 `JsonWebKeys` 并按 `kid` 取 `JsonWebKey`，供 `JsonWebToken.VerifySignature` 使用。

## 输入输出规范

### OpenIdMetadata（record，发现文档模型）
字段（`[JsonPropertyName]` 映射 RFC 8414 发现文档）：
- `required string Issuer`（`issuer`）
- `string? AuthorizationEndpoint`（`authorization_endpoint`）
- `string? DeviceAuthorizationEndpoint`（`device_authorization_endpoint`）
- `required string TokenEndpoint`（`token_endpoint`）
- `required string UserInfoEndpoint`（`userinfo_endpoint`）
- `string? RegistrationEndpoint`（`registration_endpoint`）
- `required string JwksUri`（`jwks_uri`）
- `required IReadOnlyList<string> ScopesSupported`（`scopes_supported`）
- `required IReadOnlyList<string> SubjectTypesSupported`（`subject_types_supported`）
- `required IReadOnlyList<string> IdTokenSigningAlgValuesSupported`（`id_token_signing_alg_values_supported`）

### OpenIdOptions（record，配置）
属性：
- `required string OpenIdDiscoveryAddress`：发现文档地址。
- `required string ClientId`：客户端 ID（必须设置）。
- `bool OnlyDeviceAuthorize`：仅使用设备代码流（为 `true` 时授权码流不再要求 `RedirectUri`）。
- `string? RedirectUri`：回调 URI。
- `Dictionary<string,string>? Headers`：附加请求头。
- `bool EnablePkceSupport = true`：是否启用 PKCE（默认启用）。
- `required Func<HttpClient> GetClient`：获取 `HttpClient`，生命周期由调用方管理。
- `OpenIdMetadata? Meta { internal set; }`：**不要手动设置**，由 `InitializeAsync` 填充。

方法签名：
- `virtual Task InitializeAsync(CancellationToken token)`：GET 发现地址 → `AsJsonAsync<OpenIdMetadata>`。
- `Task<JsonWebKey> GetSignatureKeyAsync(string kid, CancellationToken token)`：GET `JwksUri` → 取 `Keys.SingleOrDefault(k => k.Kid == kid)`，找不到抛 `IdentityModelConfigurationException`。
- `virtual OAuthClientOptions BuildOAuthOptions()`：校验 `Meta` 非空、`TokenEndpoint` 非空；若非 `OnlyDeviceAuthorize` 且 `RedirectUri` 为空则抛异常；返回 `OAuthClientOptions`（含 `ClientId`、`RedirectUri`、`GetClient`、`Meta` 形式的 `EndpointMeta`）。

### OpenIdClient（class : IOAuthClient）
- 构造：`OpenIdClient(OpenIdOptions options)`。
- `Task InitializeAsync(CancellationToken token, bool checkAddress = false)`：见运行流程。
- `string GetAuthorizeUrl(string[] scopes, string state, Dictionary<string,string>? extData = null)`
- `Task<AuthorizeResult?> AuthorizeWithCodeAsync(string code, CancellationToken token, Dictionary<string,string>? extData = null)`
- `Task<DeviceCodeData?> GetCodePairAsync(string[] scopes, CancellationToken token, Dictionary<string,string>? extData = null)`
- `Task<AuthorizeResult?> AuthorizeWithDeviceAsync(DeviceCodeData data, CancellationToken token, Dictionary<string,string>? extData = null)`
- `Task<AuthorizeResult?> AuthorizeWithSilentAsync(AuthorizeResult data, CancellationToken token, Dictionary<string,string>? extData = null)`

## 使用约束与注意事项

- **必须先 `InitializeAsync`**：之后所有方法在未初始化时抛 `IdentityModelConfigurationException("请先调用 InitializeAsync() …")`。`Meta` 仅供内部读取，不要手动赋值。
- **ClientId 必填**；授权码流（`OnlyDeviceAuthorize == false`）必须设置 `RedirectUri`，否则 `BuildOAuthOptions` 抛异常。
- **PKCE 默认开启**：`EnablePkceSupport = true` 时授权码流走 `PkceClient`，会自动附加 `code_challenge`/`code_challenge_method`；设备流与刷新流不受影响。
- **底层客户端可替换**：通过 `EnablePkceSupport` 切换，但二者都实现 `IOAuthClient`，调用方无需关心差异。
- **与 Profile/Yggdrasil 的依赖**：`YggdrasilConnect` 继承本目录类型（见同层 `YggdrasilConnect`），`JsonWebToken` 依赖 `OpenIdMetadata` 做 issuer/算法校验，密钥经 `GetSignatureKeyAsync` 取得。
- **异常体系**：配置/元数据相关错误统一抛 `IdentityModelConfigurationException`（基类 `IdentityModelException`）。
- **HttpClient 管理**：`GetClient` 委托给调用方提供与释放，本目录不持有其生命周期。
