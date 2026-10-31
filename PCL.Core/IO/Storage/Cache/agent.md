# IO/Storage/Cache 目录

缓存服务子系统，位于命名空间 `PCL.Core.IO.Storage.Cache`。提供统一的缓存抽象，支持策略、逐出、统计，并以 SQLite 或文件作为持久化后端。

## 本目录模块

- **`ICacheService.cs`** — 缓存服务接口 `ICacheService`：定义缓存的读写、删除、统计等契约。
- **`CacheService.cs`** — 缓存服务实现 `CacheService`（核心）：`InitializeAsync` 初始化，`SetAsync`/`GetAsync`/`ExistsAsync`/`DeleteAsync` 基本操作，`CacheFileAsync`/`GetCachedFilePathAsync` 文件缓存，`DeleteByGroupAsync`/`DeleteByTagAsync`/`DeleteExpiredAsync` 批量清理，`GetStatsAsync`/`ClearAsync`/`CompactAsync` 统计与维护，以及 `UpsertInstanceAsync`/`LookupInstanceAsync`/`DeleteInstanceAsync`/`UpsertComponentAsync` 实例与组件缓存。
- **`CacheServiceManager.cs`** — 缓存服务管理器 `CacheServiceManager`：协调多命名空间/多实例的缓存服务。
- **`CacheOptions.cs`** — 缓存选项 `CacheOptions`：TTL、容量、内联阈值等配置。
- **`CachePolicy.cs`** — 缓存策略 `CachePolicy`：命中/逐出规则，配合 `CachePriority`、`CacheStorageMode`。
- **`CacheKeys.cs`** — 缓存键常量 `CacheKeys`：`InstanceMeta`/`InstanceManifest`/`InstanceComponents`/`Download`/`Library`/`AssetIndex`/`AssetObject`/`ApiResponse`/`Skin`/`Image`/`News`/`AuthToken`/`AccountList` 等统一键。
- **`CacheResult.cs`** — 缓存结果 `CacheResult`：包装命中状态（`Hit`）与值。
- **`CacheStats.cs`** — 缓存统计 `CacheStats`：命中率、条目数、容量等。
- **`CacheEvictionService.cs`** — 逐出服务 `CacheEvictionService`：`Start`/`Stop` 后台运行，`CheckThreshold` 触发按策略清理，并抛出 `CacheEntryEvictedEventArgs`。
- **`SchemaManager.cs`** — Schema 管理 `SchemaManager`：`EnsureCurrentSchemaAsync` 建表/迁移与版本管理。
- **`SqliteCacheStorage.cs`** — SQLite 存储 `SqliteCacheStorage`：`UpsertAsync`/`LookupAsync`/`TouchAsync`/`DeleteAsync`、分组/标签/过期删除、`GetStatsAsync`、`GetEvictionCandidatesAsync`、`CompactAsync` 及实例/组件表操作。
- **`FileCacheStora.cs`** — 文件存储 `FileCacheStorage`：`StoreAsync`/`Retrieve`/`GetFilePath`/`Exists`/`ReleaseAsync`/`ForceDeleteAsync`/`Dispose`，负责大对象的文件引用与释放（`ReleaseResult`）。

## 直接代码要点

- **运行流程**：`CacheService.InitializeAsync` → `SchemaManager` 确保库结构 → 读写经 `CacheService` 判定内联/文件引用 → `SqliteCacheStorage` 持久化元数据、`FileCacheStorage` 保存大对象 → `CacheEvictionService` 按阈值后台逐出 → `GetStatsAsync` 观测命中率。
- **输入输出规范**：键为字符串（建议用 `CacheKeys` 常量），值为字节/流/文件；结果为 `CacheResult`（含命中标记）；统计为 `CacheStats`。
- **使用约束与注意事项**：必须先初始化再读写；SQLite 与文件存储需保证并发安全；TTL/容量策略由 `CacheOptions`/`CachePolicy` 决定；删除/逐出后注意文件引用计数；`Dispose` 释放数据库与文件句柄。

## 子目录导航

- [Model](./Model/agent.md)：缓存条目与缓存行模型。
