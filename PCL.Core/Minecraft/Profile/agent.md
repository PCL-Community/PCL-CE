# Minecraft/Profile 目录

游戏档案（账号）子系统，位于命名空间 `PCL.Core.Minecraft.Profile`。管理玩家账号档案的存储与认证，聚合多种认证提供者（Microsoft、Yggdrasil Connect、Authlib）。

## 本目录模块

本目录为档案子系统的容器层，提供档案的泛型内存管理（`ProfileManagement`）与面向 `McProfile` 的生命周期服务（`ProfileService`），具体的认证实现位于 `Authentication` 子目录，数据模型位于 `Models` 子目录。

- **ProfileManagement.cs**：泛型档案内存管理器 `ProfileManagement<TProfileModel>`（约束 `TProfileModel : SafeProfile`）。
  - 职责：以线程安全（`lock _syncLock`）方式维护一份档案列表与「当前选中」档案，并提供增删改查、序列化与加载。
  - 关键成员：`_profiles`（`ProfileJson<TProfileModel>`）、`_current`、`LastUsed`；`GetAll`、`Add`（重复 id 抛 `InvalidOperationException`）、`Delete`（同步 `LastUsed` 索引）、`Update`、`Select`/`SelectAt`（维护 `LastUsed`）、`LoadFromString`（校验重复 id，否则抛 `InvalidDataException` ）、`Serialize`、`Clear`。
  - 角色：档案持久化前的内存态；被 `ProfileService` 直接持有（实例化为 `ProfileManagement<McProfile>`）。`ProfileJson`/`SafeProfile` 等类型来自 `Models` 子目录。
- **ProfileService.cs**：档案生命周期服务 `ProfileService`（partial，`[LifecycleService(LifecycleState.Loaded)]`、`[LifecycleScope("profile", "档案服务")]`）。
  - 职责：作为档案子系统对外的静态门面，负责加载/保存档案配置、转发增删改选操作，并编排认证流程。
  - 关键成员：静态 `_profiles = new ProfileManagement<McProfile>()`；`Load`/`Save`（通过 `ConfigService` 读写 `ConfigSource.SharedEncrypt`/`Shared`，失败时关闭持久化以避免覆盖原始凭据）；`Add`/`Update`/`Remove`/`Select`/`SelectAt`/`Clear`；`AuthenticateAsync(ProfileType, AuthenticationRequest, existing, select, ct)`——处理令牌未过期直接复用、按类型创建 provider（`_CreateProviderAsync`）、优先 `ValidateAsync`/`RefreshAsync` 再必要时 `AuthenticateAsync`，并用 `ApplyAuthenticationResult` 落库；`_TryMigrateLegacy` 从旧 `profiles.json` 迁移；`_Decrypt` 借助 `EncryptHelper.SecretDecrypt` 解密旧字段。
  - 角色：连接 UI/启动流程与下层 `Authentication` 提供者；依赖 `Authentication`、`Models`、`IdentityModel`、`Secret` 等。

## 直接代码要点

- **运行流程（认证）**：`ProfileService.AuthenticateAsync` → 若现有档案令牌有效且未强制刷新则直接复用 → 否则 `_CreateProviderAsync` 构造对应 provider → 尝试 `ValidateAsync`/`RefreshAsync`（捕获 `IdentityModelAuthenticationException`，对 `xsts_` 与 `invalid_grant` 特殊处理，可经 `RefreshFailureHandler` 回调）→ 仍失败则 `provider.AuthenticateAsync` → `ApplyAuthenticationResult` 写回档案并（可选）选中。
- **输入与输出规范**：档案以 JSON 存于加密/共享配置；`AuthenticateAsync` 输入 `ProfileType` 与 `AuthenticationRequest`（含用户名/密码/刷新令牌/device-code 回调/`ProfileSelector` 等），输出 `McProfile`；返回前会补 `ProfileId`（Guid）与 `LastUsed` 维护。
- **使用约束与注意事项**：若加密配置不可读或格式无效，`_canPersist` 会被置为 `false` 以「不覆盖原始凭据」，此后 `Save` 为空操作；`Load` 在已加载时幂等；`AuthenticateAsync` 中 `Authlib` 类型在未强制重选时优先走 `ValidateAsync`；迁移逻辑对解密失败的单字段回退为原文，不整体失败。

## 子目录导航

- [Authentication](./Authentication/agent.md)：认证提供者。
- [Models](./Models/agent.md)：档案数据模型。
