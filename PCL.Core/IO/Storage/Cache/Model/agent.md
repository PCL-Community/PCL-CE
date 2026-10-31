# IO/Storage/Cache/Model 目录

缓存数据模型，位于命名空间 `PCL.Core.IO.Storage.Cache.Model`。这些是不可变记录（record），描述缓存子系统持久化到 SQLite / 文件存储中的实体结构，由 `PCL.Core.IO.Storage.Cache` 下的 `SchemaManager`、`SqliteCacheStorage`、`CacheService` 等使用。

## 本目录模块

- **CacheEntry.cs**：通用缓存条目模型。
- **ComponentCacheRow.cs**：组件级缓存行（组件维度的缓存记录）。
- **InstanceCacheRow.cs**：实例级缓存行（启动器实例维度的缓存记录）。

## 运行流程

本目录只是**数据模型**（纯 record），本身不含业务逻辑，运行流程体现在调用方：

- `CacheEntry` 是 `ICacheService` 底层存储的通用实体：写入时根据 `EntryType` 决定数据存在 `Data`（内联，`Inline`）还是以 `FileHash`/`FilePath` 引用缓存目录下的文件（`FileRef`）；读取时由 `SqliteCacheStorage` 还原为对象再经 `CacheResult<T>` 返回。
- `ComponentCacheRow` 与 `InstanceCacheRow` 是「实例扫描缓存」表结构，由实例/组件扫描流程写入（记录每个 `.minecraft` 实例的元信息、组件清单与扫描指纹），避免每次启动都重新遍历磁盘。它们通常通过 `CacheService` 的 `SetAsync`/`GetAsync` 以对象形式存取，由 `SchemaManager` 管理表结构迁移（`CacheVersion`/`FormatVersion` 充当 schema 版本号）。

## 输入输出规范

### CacheEntry（`CacheEntry.cs`）
位置记录，所有字段均为 `init`（构造后不可变）：
- `string CacheKey`：缓存键（注释为 SHA-256 Raw key），即 `CacheKeys` 计算出的键。
- `EntryType EntryType`：`Inline = 0`（数据内联）/ `FileRef = 1`（文件引用）。
- `string ContentType`：MIME 类型。
- `byte[]? Data`：内联模式下的缓存数据；`DataSize` 为原始数据大小。
- `string? FileHash` / `string? FilePath`：文件引用模式下的 SHA-256 哈希与（缓存目录内）相对路径。
- `string? ContentHash`：内容哈希；`int ContentVersion`：内容格式版本（用于迁移，读取端按版本解析）。
- 时间戳：`DateTime CachedAt`、`LastAccessAt`、`DateTime? ExpiresAt`；`long HitCount` 命中计数。
- `string Tags` / `string GroupName`：标签（逗号分隔）与分组，对应 `CachePolicy` 的批量淘汰维度。
- `int Priority`：`0=Low; 1=Normal; 2=High; 3=NeverEvict`（对应 `CachePriority`）。

### EntryType（`CacheEntry.cs`）
`enum EntryType { Inline = 0, FileRef = 1 }`——决定 `Data` 与 `FileHash`/`FilePath` 哪组字段有效。

### ComponentCacheRow（`ComponentCacheRow.cs`）
位置记录，复合主键由三段组成（`init`）：
- PK：`string InstancePath`、`string CompType`（mods/rp/shader/saves/datapack）、`string FileName`；
- `string RelativePath`、`string? FileHash`（SHA256）、`long FileSize`、`DateTime LastModified`、`bool Enabled`；
- Mod 特有字段（JSON 序列化到 `ModMetadata` 列）：`ModName`/`ModVersion`/`ModAuthor`/`ModDescription`/`ModLoader`（forge/fabric/quilt/liteloader）/ `ModDependencies`（JSON 数组）；
- 扫描元数据：`int CacheVersion = 7`（schema 标记）、`DateTime ScannedAt`、`string? ScanHash`（目录扫描指纹，用于判断是否需要重扫）。

### InstanceCacheRow（`InstanceCacheRow.cs`）
位置记录，实例级元数据：
- 基础：`InstancePath`、`InstanceName`、`InstanceState`（默认 `"Error"`）、`int CardType`、`bool IsStarred`、`Logo?`、`Description?`、`ReleaseTime?`、`VanillaName?`、`VanillaVersion?`、`int DropNumber`、`bool Reliable`；
- 加载器：`string LoaderJson`（**internal set**）统一 JSON 存储：`[{"type":"forge","version":"47.2.0"}, ...]`，空数组表示无加载器；配套方法：
  - `List<LoaderEntry> GetLoaders()`：反序列化 `LoaderJson`，失败返回空列表；
  - `bool HasLoader(string type)` / `string? GetLoaderVersion(string type)`：快速查询；
  - `void SetLoaders(List<LoaderEntry> loaders)`：序列化回 `LoaderJson`。
- Manifest 摘要：`MainClass?`、`AssetsIndex?`、`InheritsFrom?`、`int? JavaVersion`；
- 缓存控制：`SourceJsonHash`（version.json 的 SHA256）、`int FormatVersion = 1`、`DateTime CachedAt`、`DateTime? LastLoadedAt`。

### LoaderEntry（`InstanceCacheRow.cs`）
位置记录：`string Type`（forge/fabric/quilt/neoforge/liteloader/optifine/labymod/cleanroom/legacyfabric 等）、`string? Version`（空串=已检测但版本未知，null=不存在）。新增加载器类型只需在此加条目，无需改表结构。

## 使用约束与注意事项

- **不可变性**：除 `InstanceCacheRow.LoaderJson`（因需反序列化写入而设为 `internal set`）外，字段几乎都是 `init`；要"修改"一行需整体重建记录再写回，不要试图直接改字段。
- **JSON 容错**：`GetLoaders()` 在 `LoaderJson` 损坏时返回 `[]` 而非抛异常；写入方应确保 `SetLoaders` 产出合法 JSON。
- **版本号语义**：`ComponentCacheRow.CacheVersion`（默认 7）与 `InstanceCacheRow.FormatVersion`（默认 1）是 schema 标记，数据结构变更时应同步递增，配合 `SchemaManager` 做迁移；硬编码默认值暗示历史已多次迁移。
- **依赖关系**：本目录命名空间 `PCL.Core.IO.Storage.Cache.Model`；实际读写由 `PCL.Core.IO.Storage.Cache` 中的 `CacheService`/`SqliteCacheStorage`/`SchemaManager` 完成；`CacheEntry` 与 `CachePolicy`（`Group`/`Tags`/`Priority`）字段相互呼应。
- **存储分层**：`CacheEntry` 的 `Inline`/`FileRef` 由 `ICacheService.SetAsync` 依数据大小（≤256KiB 内联，否则文件映射，`CacheStorageMode.Auto`）自动选择，调用方一般无需手动决定 `EntryType`。
- **待确认**：`ComponentCacheRow`/`InstanceCacheRow` 的具体写入触发点（实例扫描器位置）与 SQLite 表名映射未在本次阅读范围内，引用名与列名以代码注释为准。
