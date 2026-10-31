# Minecraft/Profile/Authentication 目录

档案认证子系统，位于命名空间 `PCL.Core.Minecraft.Profile.Authentication`。定义统一的认证提供者接口与多种实现（Microsoft、Yggdrasil Connect、Authlib 注入）。

## 本目录模块

- **`IAuthenticateProvider.cs`** — 认证提供者接口 `IAuthenticateProvider`：定义登录/刷新/校验契约；配套 `AuthenticationRequest`、`AuthenticationCandidate`、`DeviceCodeAuthenticationContext`（设备码流程上下文）、`AuthenticationResult`（认证结果）。
- **`MicrosoftProvider.cs`** — Microsoft 认证 `MicrosoftProvider`：`GetDeviceCodeAsync`/`PollDeviceCodeAsync` 设备码登录，`AuthenticateAsync`/`RefreshAsync`/`ValidateAsync` 登录与令牌维护，`RefreshOAuthAsync` 刷新 OAuth，`CompleteAsync` 完成流程。
- **`YggdrasilConnectProvider.cs`** — Yggdrasil Connect 认证 `YggdrasilConnectProvider`：`InitializeAsync` 初始化、`GetCodePairAsync`/`PollDeviceCodeAsync` 设备码、`AuthenticateAsync`/`RefreshAsync`/`ValidateAsync`；`TryGetBuiltInClientId` 取内置 ClientId，`YggdrasilConnectClaims` 承载声明。
- **`AuthlibProvider.cs`** — Authlib 注入认证 `AuthlibProvider`：`AuthenticateAsync`/`RefreshAsync`/`ValidateAsync`，面向第三方认证服（authlib-injector）。

## 直接代码要点

- **运行流程**：选择提供者 → 设备码/密码等方式认证 → 获得令牌与档案 → 后续 `RefreshAsync` 续期、`ValidateAsync` 校验；Microsoft 走 Xbox→XSTS→Mojang 链路，Yggdrasil Connect 走 OpenID Connect 设备流。
- **输入输出规范**：输入为 `AuthenticationRequest`（账号/凭据/ClientId 等）；输出 `AuthenticationResult`（令牌、档案、过期时间）；异步方法支持取消。
- **使用约束与注意事项**：各提供者令牌格式与刷新策略不同；设备码需轮询并处理过期；ClientId 可回退内置值；敏感令牌不得明文落盘；网络异常需可重试。

## 子目录导航

- [Utils](./Utils/agent.md)：认证辅助（Mojang/Xbox）。
