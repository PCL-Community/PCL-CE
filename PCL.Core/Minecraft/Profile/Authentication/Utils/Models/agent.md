# Minecraft/Profile/Authentication/Utils/Models 目录

Mojang/Xbox 认证数据模型，位于命名空间 `PCL.Core.Minecraft.Profile.Authentication.Utils.Models`。这些是纯 DTO（数据传输对象），仅承载微软账号 → Xbox Live → XSTS → Mojang 认证链中各步骤的请求/响应字段，不含业务逻辑。业务逻辑与 HTTP 调用在 `Utils/XboxUtils.cs`、`Utils/MojangUtils.cs` 以及 `MicrosoftProvider.cs` 中。

## 本目录模块

- **MojangIdentityToken.cs**：Mojang 身份令牌模型。
- **MojangProfile.cs**：Mojang 档案模型。
- **MojangResponse.cs**：Mojang 响应模型。
- **XboxAuthenticate.cs**：Xbox 认证请求/响应模型。
- **XboxLiveResponse.cs**：Xbox Live 响应模型。
- **XboxDisplayClaims.cs**：Xbox 显示声明模型。
- **XboxProperty.cs**：Xbox 属性模型。
- **XSTSProperty.cs**：XSTS 属性模型。
- **XboxUserHash.cs**：Xbox 用户哈希模型。

## 运行流程

这些模型串起微软账号登录 Minecraft 的「Xbox 三步走」链路（由 `XboxUtils` 与 `MojangUtils` 驱动）：

1. **Xbox Live 认证**：用微软 OAuth 访问令牌作为 `RpsTicket`，构造 `XboxAuthenticate<XboxProperty>`（`AuthMethod="RPS"`、`SiteName="user.auth.xboxlive.com"`），`XboxUtils.AuthenticateAsync(..., isXsts: false)` → 返回 `XboxLiveResponse`（含 `Token` 与 `DisplayClaims.Xui[0].uhs`）。
2. **XSTS 授权**：以第 1 步的 `uhs` 作为 `UserTokens`，构造 `XboxAuthenticate<XSTSProperty>`（`SandboxId="RETAIL"`），`XboxUtils.AuthenticateAsync(..., isXsts: true)` → 返回新的 `XboxLiveResponse`（含 XSTS `Token` 与 `uhs`）。XSTS 失败时抛 `IdentityModelAuthenticationException("xsts_<XErr>", …)`。
3. **Mojang 登录**：以 `XBL3.0 x={uhs};{xstsToken}` 形式组装 `MojangIdentityToken.IdentityToken`，`MojangUtils.AuthenticateAsync(xstsToken, userHash)` → 返回 `MojangResponse`（含 `AccessToken`、`ExpiresIn`、`TokenType`、`UserName`、`Roles`）。
4. **获取档案**：`MojangUtils.GetProfileAsync(...)` → 返回 `MojangProfile`（含 `Uuid`、`Name`、`Properties`）；未创建 Java 档案时抛 `IdentityModelAuthenticationException("profile_not_created", …)`。

> 注：本目录的 Xbox/Mojang 模型服务于**微软账号**登录链路；Yggdrasil Connect（第三方账号）走的是另一套基于 `JsonWebToken`/`id_token` 的链路，二者不混用。

## 输入输出规范

### 请求模型
- **XboxAuthenticate&lt;T&gt;**（record，泛型）：
  - `required T Properties`：随请求携带的属性（通常为 `XboxProperty` 或 `XSTSProperty`）。
  - `string RelyingParty = "http://auth.xboxlive.com"`：依赖方，默认 Xbox Live。
  - `required string TokenType`：令牌类型。
- **XboxProperty**（record）：`AuthMethod = "RPS"`、`SiteName = "user.auth.xboxlive.com"`、`required string RpsTicket`（微软访问令牌）。
- **XSTSProperty**（record）：`SandboxId = "RETAIL"`、`List<string> UserTokens = []`（填入上一步的 `uhs`）。
- **MojangIdentityToken**（class）：`[JsonPropertyName("identityToken")] required string IdentityToken`。

### 响应模型
- **XboxLiveResponse**（record）：`required DateTime IssueInstant`、`required DateTime NotAfter`、`required string Token`、`[JsonPropertyName("DisplayClaims")] XboxDisplayClaims? DisplayClaims`。
- **XboxDisplayClaims**（record）：`[JsonPropertyName("xui")] required List<XboxUserHash> Xui`；用户哈希通常取 `Xui[0].UserHash`。
- **XboxUserHash**（record）：`[JsonPropertyName("uhs")] required string UserHash`。
- **MojangResponse**（record）：`UserName`(`username`)、`Roles`(`roles`)、`AccessToken`(`access_token`)、`ExpiresIn`(`expires_in`, int)、`TokenType`(`token_type`)。
- **MojangProfile**（record）：`Uuid`(`id`)、`Name`(`name`)、`List<PlayerProperty>? Properties`（来自 `PCL.Core.Minecraft.IdentityModel.Yggdrasil` 命名空间）、`[JsonExtensionData] Dictionary<string,JsonElement>? AdditionalData`（吸收未知字段）。

### 序列化说明
- 全部基于 `System.Text.Json` 的 `[JsonPropertyName]` 映射；`MojangProfile` 额外用 `[JsonExtensionData]` 兜住协议扩展字段。
- `XboxLiveResponse.DisplayClaims` 的 JSON 字段名为大写 `DisplayClaims`（而非小驼峰），反序列化依赖该特性。

## 使用约束与注意事项

- **纯数据、无行为**：这些模型只描述字段，不含校验或 HTTP 逻辑；请求构造与响应解析都在 `XboxUtils`/`MojangUtils` 中。
- **uhs 取自 DisplayClaims**：Xbox/XSTS 的「用户哈希」来自 `XboxLiveResponse.DisplayClaims.Xui[0].UserHash`，而非顶级字段；缺失 display claims 时 Microsoft 链路会失败。
- **TokenType 字段类型不同**：`XboxAuthenticate.TokenType` 是 string 请求字段；`MojangResponse.TokenType` 是响应里的令牌类型（应为 `"Bearer"`）。注意区分命名空间与用途。
- **时间字段**：`XboxLiveResponse.IssueInstant`/`NotAfter` 为 `DateTime`，由 JSON 反序列化直接映射；令牌有效期判断应结合 `MojangResponse.ExpiresIn`（秒）与 `XboxLiveResponse.NotAfter`。
- **Profile 复用 Yggdrasil 类型**：`MojangProfile.Properties` 的元素类型是 `PCL.Core.Minecraft.IdentityModel.Yggdrasil.PlayerProperty`（带 `Name`/`Value`/`Signature`），与 Yggdrasil 传统认证共用，便于统一处理皮肤/披风等属性。
- **错误映射**：XSTS 错误以数值 `XErr` 抛出 `xsts_<XErr>` 异常（见 `XboxUtils`）；Mojang 档案未创建抛 `profile_not_created`。这些异常定义不在本目录，而在 `IdentityModel` 异常体系。
- **密钥/机密**：本目录不含任何凭据或 `PCL.Core.Utils.Secret` 引用；访问令牌等敏感串以普通 `string` 持有（与 `AuthorizeResult` 同样未用 `SecureString`，源码注释说明因 DPAPI 依赖非最佳实践）。
