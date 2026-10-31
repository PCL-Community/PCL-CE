# Minecraft/IdentityModel 目录

身份认证模型子系统，位于命名空间 `PCL.Core.Minecraft.IdentityModel`。封装与 Minecraft 账号认证相关的协议客户端（OAuth、Yggdrasil、JWT、OpenID Connect、PKCE），并为各扩展实现提供统一异常。

## 本目录模块

本目录为异常与协议基元的容器层，仅直接包含统一的认证异常类型，具体的协议客户端实现位于 `Extensions`、`OAuth`、`Yggdrasil` 等子目录。

- **IdentityModelException.cs**：认证模型统一异常类型集合。
  - 职责：为下层 OAuth/Yggdrasil/JWT 等协议调用提供可分类的异常层次，使上层 `Profile` 可按错误码（如 `invalid_grant`、`xsts_*`）做针对性处理，而无需依赖消息文本。
  - 关键类型：
    - `IdentityModelException(string message, Exception? innerException)`：所有认证异常的基类。
    - `IdentityModelConfigurationException`：配置/元数据不满足认证流程要求时抛出（如未初始化、缺少 client id）。
    - `IdentityModelAuthenticationException(string? error, string? errorDescription, Exception? innerException)`：认证服务器返回 OAuth/Yggdrasil 协议错误时抛出，携带 `Error` 与 `ErrorDescription` 两个只读属性，`_BuildMessage` 负责拼装可读信息。
  - 角色：被 `Profile/Authentication` 下的 `MicrosoftProvider`、`YggdrasilConnectProvider`、`AuthlibProvider` 以及 `Utils` 中的认证辅助广泛抛出并捕获。

## 直接代码要点

- **运行流程**：下层协议实现在失败或配置缺失时构造对应的 `IdentityModel*` 异常向上抛出；上层 `ProfileService.AuthenticateAsync` 等按 `Error` 字段决定是重试、走刷新失败处理器还是直接抛出。
- **输入与输出规范**：异常构造输入为协议错误码/描述与内部异常；上层通过 `Error`/`ErrorDescription` 取值做分支判断，而非字符串匹配。
- **使用约束与注意事项**：`IdentityModelAuthenticationException` 的 `Error` 可能包含 `xsts_<XErr>` 形式（来自 Xbox/XSTS 链路），上层据此区分可重试与不可重试错误；`IdentityModelConfigurationException` 表示流程未就绪，不应被当作普通认证失败静默吞掉。

## 子目录导航

- [Extensions](./Extensions/agent.md)：认证协议扩展（JWT、OpenID、PKCE、YggdrasilConnect）。
- [OAuth](./OAuth/agent.md)：OAuth 客户端。
- [Yggdrasil](./Yggdrasil/agent.md)：Yggdrasil 客户端。
