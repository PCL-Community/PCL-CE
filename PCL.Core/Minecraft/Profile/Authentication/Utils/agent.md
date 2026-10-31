# Minecraft/Profile/Authentication/Utils 目录

认证辅助，位于命名空间 `PCL.Core.Minecraft.Profile.Authentication.Utils`。封装 Microsoft/Xbox 认证链路中的底层调用。

## 本目录模块

- **`MojangUtils.cs`** — Mojang 认证辅助 `MojangUtils`：`AuthenticateAsync` 用 Xbox 令牌换取 Mojang 访问令牌，`CheckLicenseAsync` 校验游戏所有权，`GetProfileAsync` 获取玩家档案。
- **`XboxUtils.cs`** — Xbox Live 认证辅助 `XboxUtils`：`AuthenticateAsync` 完成 Xbox Live/XSTS 认证并返回用户哈希与令牌。

## 直接代码要点

- **运行流程**：Xbox Live 认证 → XSTS 授权（`XboxUtils.AuthenticateAsync`）→ Mojang 登录换取令牌（`MojangUtils.AuthenticateAsync`）→ 校验所有权（`CheckLicenseAsync`）→ 拉取档案（`GetProfileAsync`）。
- **输入输出规范**：输入为 Xbox/XSTS 令牌或凭据；输出为 Mojang 访问令牌、所有权结果、`MojangProfile`（见 `Models`）。
- **使用约束与注意事项**：链路各步令牌有有效期与受众（RelyingParty）约束；错误码需映射为可读原因（如 `xsts_*`、`profile_not_created`）；敏感令牌不得记录到日志。

## 子目录导航

- [Models](./Models/agent.md)：Mojang/Xbox 认证数据模型。
