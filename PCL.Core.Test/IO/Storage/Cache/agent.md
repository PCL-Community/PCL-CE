# IO/Storage/Cache 测试

本目录测试 PCL.Core 的缓存系统：缓存服务 `CacheService`、SQLite 元数据层 `SqliteCacheStorage`、文件引用层 `FileCacheStorage`，以及它们之间的集成。测试均使用临时目录，并在 `TestCleanup` 中清理。

## 本目录测试模块

- `CacheServiceTest.cs` — `CacheService` 核心功能：Set/Get、绝对过期与永不过期策略、内联 vs 文件引用存储（按 `MaxInlineSize` 分流）、删除、存在性、分组（Group）与标签（Tag）批量删除、过期清理、整体清空、统计（命中率）、`CacheFileAsync`/`GetCachedFilePathAsync`、并发线程安全（`PCL.Core.IO.Storage.Cache`）。
- `SqliteCacheStorageTest.cs` — `SqliteCacheStorage` 数据库层 CRUD、查询与文件哈希管理，含 `SchemaManager` 初始化（`PCL.Core.IO.Storage.Cache` / `.Model`）。
- `FileCacheStorageTest.cs` — `FileCacheStorage` 文件存储、引用计数与清理操作（`PCL.Core.IO.Storage.Cache`）。
- `CacheSystemIntegrationTest.cs` — 集成测试：`CacheService` 与 `SqliteCacheStorage`/`FileCacheStorage` 的端到端交互（`PCL.Core.IO.Storage` / `.Cache`）。
