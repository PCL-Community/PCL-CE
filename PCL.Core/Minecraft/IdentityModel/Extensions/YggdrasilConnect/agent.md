# Minecraft/IdentityModel/Extensions/YggdrasilConnect 目录

Yggdrasil Connect 支持，位于命名空间 `PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect`。对接新版 Yggdrasil Connect 认证端点（基于 OpenID Connect，并增加了 Minecraft 专属约束）。代码层面通过继承/复用 `OpenId` 目录实现，仅覆写发现文档校验与必需 scope。

## 本目录模块

- **YggdrasilClient.cs**：Yggdrasil Connect 客户端。
- **YggdrasilConnectMetaData.cs**：Yggdrasil Connect 元数据模型。
- **YggdrasilOptions.cs**：Yggdrasil Connect 选项配置。

## 运行流程

`YggdrasilConnect` 本质上是「OpenID Connect + 魔改」（源码注释：完全就是 OpenId + 魔改了一部分，可直接复用 OpenId 逻辑）。所有协议交互都委托给 `OpenIdClient`：

1. **初始化**：`YggdrasilClient.InitializeAsync(token)` → 新建 `OpenIdClient(_options)` 并调用其 `InitializeAsync(token, checkAddress: true)`（要求至少存在授权或设备端点）。底层 `YggdrasilOptions.InitializeAsync` 在拉取发现文档后做 HTTPS 与 scope 强校验。
2. **授权与令牌**：`GetAuthorizeUrl` / `AuthorizeWithCodeAsync` / `GetCodePairAsync` / `AuthorizeWithDeviceAsync` / `AuthorizeWithSilentAsync` 全部转发到内部 `OpenIdClient`。实际业务编排见 `Profile/Authentication/YggdrasilConnectProvider`（设备代码流为主，兑换后用 `JsonWebToken` 校验 `id_token`）。
3. **id_token 校验**：Provider 完成令牌兑换后，用 `OpenIdOptions.GetSignatureKeyAsync(kid)` 取密钥，再用 `JsonWebToken` 验签并读出 `YggdrasilConnectClaims`（含 `selectedProfile` / `availableProfiles`）。

典型入口：`YggdrasilConnectProvider` 构造 `YggdrasilOptions`，设 `OnlyDeviceAuthorize = true`，调用 `InitializeAsync` 后走设备流。

## 输入输出规范

### YggdrasilConnectMetaData（record : OpenIdMetadata）
- 继承 `OpenIdMetadata` 全部字段。
- 新增 `string? SharedClientId`（`shared_client_id`）：当未显式提供 `ClientId` 时，可用元数据中的共享客户端 ID 兜底。

### YggdrasilOptions（record : OpenIdOptions）
覆写两个方法，其余属性（如 `OpenIdDiscoveryAddress`、`ClientId`、`OnlyDeviceAuthorize`、`RedirectUri`、`Headers`、`EnablePkceSupport`、`GetClient`）继承自 `OpenIdOptions`。
- `override Task InitializeAsync(CancellationToken token)`：
  - 校验 `OpenIdDiscoveryAddress` 必须是绝对 `https` URI，否则抛 `IdentityModelConfigurationException`（"must use HTTPS"）。
  - 拉取并反序列化为 `YggdrasilConnectMetaData`（失败抛异常）。
  - `_ValidateMetadata`：校验 `Issuer` 为 https、且与发现地址同 authority（否则抛异常）；逐个校验 `AuthorizationEndpoint`/`TokenEndpoint`/`DeviceAuthorizationEndpoint`/`JwksUri`/`UserInfoEndpoint` 均为 https。
  - 校验必需 scope 齐备：`openid`、`Yggdrasil.PlayerProfiles.Select`、`Yggdrasil.Server.Join`；缺失则抛异常列出缺项。
- `override OAuthClientOptions BuildOAuthOptions()`：若 `ClientId` 为空且 `SharedClientId` 非空则采用后者；调用 `base.BuildOAuthOptions()`；最终 `ClientId` 仍为空则抛异常。

### YggdrasilClient（class : IOAuthClient）
- 构造：`YggdrasilClient(YggdrasilOptions options)`。
- `Task InitializeAsync(CancellationToken token)`：见运行流程。
- 其余五个方法与 `IOAuthClient` 一致（`GetAuthorizeUrl` / `AuthorizeWithCodeAsync` / `GetCodePairAsync` / `AuthorizeWithDeviceAsync` / `AuthorizeWithSilentAsync`），未初始化时均抛 `IdentityModelConfigurationException("请先调用 InitializeAsync() …")`。

## 使用约束与注意事项

- **强制 HTTPS**：发现地址、issuer、以及所有端点都必须为 https；discovery 与 issuer 必须同 authority，否则初始化即失败。这是相对通用 OIDC 的额外收紧。
- **必需 scope 强约束**：`openid` + `Yggdrasil.PlayerProfiles.Select` + `Yggdrasil.Server.Join` 缺一不可，否则 `InitializeAsync` 抛 `IdentityModelConfigurationException`。
- **ClientId 可来自元数据**：未设置 `ClientId` 时回退到 `SharedClientId`；两者皆空则 `BuildOAuthOptions` 抛异常。`YggdrasilConnectProvider` 还内置了按 host 查找的 `_BuiltInClientIds`（如 `littleskin.cn` → `"1514"`）。
- **复用而非重写**：所有协议流程都走 `OpenIdClient`，本目录只增加校验层；修改通用 OIDC 行为时应注意会影响 Yggdrasil Connect。
- **默认设备流**：配套 Provider 设 `OnlyDeviceAuthorize = true`，因此授权码流相关的 `RedirectUri` 要求不触发；但 `OpenIdOptions.BuildOAuthOptions` 仍要求非设备流时提供 `RedirectUri`。
- **异常体系**：统一抛 `IdentityModelConfigurationException`（配置/元数据不满足要求时）。
- **与 Profile 的依赖**：实际令牌校验、档案选择、刷新逻辑在 `Profile/Authentication/YggdrasilConnectProvider` 与 `Utils/Models`（声明模型），本目录仅负责「合规的 OIDC 客户端」。密钥/机密不在此硬编码，签名密钥走 `jwks_uri`。
