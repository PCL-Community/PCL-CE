# Comp 兼容层 — 统一 CurseForge & Modrinth API 规划

## 目标

在 C# 中设计一个统一的抽象层（Comp），屏蔽 CurseForge API（v1）和 Modrinth API（v2.7.0）的差异，为 Minecraft 第三方启动器提供一致的接口。

## 核心功能域

以下按启动器关注度从高到低排列：

| 功能域 | CurseForge | Modrinth | 启动器场景 |
|--------|-----------|----------|-----------|
| 项目搜索 | `GET /v1/mods/search` | `GET /v2/search` | 模组/整合包浏览 |
| 项目详情 | `GET /v1/mods/{modId}` | `GET /v2/project/{id\|slug}` | 展示项目信息 |
| 项目批量获取 | `POST /v1/mods` | `GET /v2/projects?ids=...` | 列表/收藏同步 |
| 文件列表 | `GET /v1/mods/{modId}/files` | `GET /v2/project/{id\|slug}/version` | 选择版本下载 |
| 文件详情 | `GET /v1/mods/{modId}/files/{fileId}` | `GET /v2/version/{id}` | 获取文件元数据 |
| 文件下载 URL | `GET /v1/mods/{modId}/files/{fileId}/download-url` | 文件对象自带 URL | 直接下载 |
| 哈希匹配 | `POST /v1/fingerprints` | `POST /v2/version_files/from_hashes` | 本地文件识别 |
| 哈希更新检测 | — | `POST /v2/version_file/{hash}/update` | 检查更新 |
| 推荐/热门 | `POST /v1/mods/featured` | `GET /v2/search?index=downloads` | 首页推荐 |
| 游戏版本列表 | `GET /v1/minecraft/version` | `GET /v2/tag/game_version` | 版本筛选 |
| ModLoader 列表 | `GET /v1/minecraft/modloader` | `GET /v2/tag/loader` | 加载器筛选 |
| 分类/标签 | `GET /v1/categories` | `GET /v2/tag/category` | 分类浏览 |
| 项目描述 | `GET /v1/mods/{modId}/description` | 项目详情自带 `body` 字段 | 展示说明 |

---

## 统一数据模型（建议）

### `CompProject`

| 字段 | CurseForge 来源 | Modrinth 来源 | 说明 |
|------|----------------|---------------|------|
| `Id` | `data.id` (int) | `id` (string base62) | 统一暴露为 string，内部保留原始类型 |
| `Provider` | `CurseForge` | `Modrinth` | 枚举标识来源 |
| `Slug` | `data.slug` | `slug` | |
| `Name` | `data.name` | `title` | |
| `Summary` | `data.summary` | `description` | 短描述 |
| `DescriptionHtml` | `GET description` 返回 | `body` (Markdown) | 长描述，注意格式差异 |
| `IconUrl` | `data.logo.url` | `icon_url` | |
| `Categories` | `data.categories[]` → 展开 | `categories[]` | |
| `GameVersions` | `data.latestFiles[].gameVersions` | `game_versions[]` | 最新版本支持的游戏版本 |
| `DownloadCount` | `data.downloadCount` | `downloads` | |
| `FollowCount` | — | `followers` | 无对应字段时返回 null |
| `ProjectType` | 固定 `mod` | `project_type` | mod/modpack/resourcepack/shader |
| `License` | `data.latestFiles[].displayName` 间接 | `license.id` | |
| `DateCreated` | `data.dateCreated` | `published` | |
| `DateModified` | `data.dateModified` | `updated` | |
| `Author` | `data.authors[0].name` | `author` (search) / team 解析 | 主作者 |
| `SiteUrl` | `data.links.websiteUrl` | — | |
| `IssuesUrl` | — | `issues_url` | |
| `SourceUrl` | `data.links.sourceUrl` | `source_url` | |
| `Status` | `data.status` 映射 | `status` | 见状态映射表 |

### `CompFile`

| 字段 | CurseForge 来源 | Modrinth 来源 |
|------|----------------|---------------|
| `Id` | `file.fileId` / `file.id` (int) | `id` (base62) |
| `ProjectId` | `modId` | `project_id` |
| `DisplayName` | `displayName` | `name` |
| `FileName` | `fileName` | `files[].filename` |
| `DownloadUrl` | `downloadUrl` | `files[].url` |
| `FileLength` | `fileLength` | `files[].size` |
| `ReleaseType` | `releaseType` (Release/Beta/Alpha) | `version_type` (release/beta/alpha) |
| `GameVersions` | `gameVersions[]` | `game_versions[]` |
| `Loaders` | `sortableGameVersions[].modLoader` → ModLoaderType | `loaders[]` |
| `Hashes` | `hashes[]` (Sha1/Md5) | `files[].hashes` (sha1/sha512) |
| `Dependencies` | `dependencies[]` | `dependencies[]` |
| `Changelog` | `GET /files/{fileId}/changelog` | `changelog` |
| `DatePublished` | `fileDate` | `date_published` |
| `DownloadCount` | `downloadCount` | `downloads` |
| `IsAvailable` | `isAvailable` | `status == "listed"` |

### `CompSearchResult`

```csharp
class CompSearchResult {
    List<CompProject> Hits { get; set; }
    int TotalCount { get; set; }
    int Offset { get; set; }
    int Limit { get; set; }
}
```

### `CompSearchFilter`

```csharp
class CompSearchFilter {
    string? Query;           // 搜索关键词
    string? GameVersion;     // 如 "1.20.1"
    List<ModLoaderType> Loaders;  // Forge, Fabric, Quilt, NeoForge
    string? Category;        // 分类 slug 或 id
    CompProjectType? ProjectType;  // mod, modpack, resourcepack 等
    CompSortField SortField;       // Relevance, Downloads, Follows, Updated, Created
    SortOrder SortOrder;           // Asc, Desc
    int Offset;              // 分页偏移
    int Limit;               // 每页数量
}
```

---

## 核心接口定义（建议）

### `ICompClient`

```csharp
interface ICompClient {
    // ===== 项目 =====
    Task<CompSearchResult> SearchProjects(CompSearchFilter filter, CancellationToken ct = default);
    Task<CompProject> GetProject(string projectId, CancellationToken ct = default);
    Task<List<CompProject>> GetProjects(IEnumerable<string> projectIds, CancellationToken ct = default);
    Task<List<CompProject>> GetFeaturedProjects(string? gameVersion = null, CancellationToken ct = default);
    Task<string> GetProjectDescription(string projectId, CancellationToken ct = default);

    // ===== 文件/版本 =====
    Task<List<CompFile>> GetProjectFiles(string projectId, CompFileFilter? filter = null, CancellationToken ct = default);
    Task<CompFile> GetFile(string fileId, CancellationToken ct = default);
    Task<string> GetFileDownloadUrl(string fileId, CancellationToken ct = default);  // 主要对 CF 需要
    Task<string> GetFileChangelog(string fileId, CancellationToken ct = default);

    // ===== 哈希匹配 =====
    Task<Dictionary<string, List<CompFile>>> MatchFingerprints(IEnumerable<string> hashes, HashAlgorithm algo, CancellationToken ct = default);
    Task<CompFile?> CheckForUpdates(string fileHash, HashAlgorithm algo, CompUpdateFilter? filter = null, CancellationToken ct = default);

    // ===== 元数据 =====
    Task<List<CompGameVersion>> GetGameVersions(CancellationToken ct = default);
    Task<List<CompLoader>> GetLoaders(CancellationToken ct = default);
    Task<List<CompCategory>> GetCategories(CancellationToken ct = default);
}
```

### `ICompClientFactory`

```csharp
interface ICompClientFactory {
    ICompClient CreateCurseForgeClient(string apiKey);
    ICompClient CreateModrinthClient(string? accessToken = null);
    ICompClient CreateAggregateClient(params ICompClient[] clients); // 统一搜索多个源
}
```

---

## 关键差异与处理策略

### 1. 认证方式

| | CurseForge | Modrinth |
|--|-----------|----------|
| 方式 | `x-api-key` header | `Authorization: mrp_...` header |
| 必要性 | 所有请求必须 | 读操作可选，写操作需认证 |
| Scope | 无 | OAuth scope 体系 |

**策略**：`CompClient` 构造时接收凭证参数，自动注入对应的认证 header。

### 2. 项目 ID 体系

| | CurseForge | Modrinth |
|--|-----------|----------|
| 类型 | 自增 int32（如 `12345`） | 8 位 base62 字符串（如 `AABBCCDD`） |
| 长度范围 | 1 ~ ~20 亿 | 固定 8 字符 |
| 前缀区分 | 无 | 无官方前缀，但可以约定使用 `mr-` / `cf-` 前缀统一标识 |

**策略**：统一暴露为 `string`。提供辅助方法 `ProjectId.TryParse()` 支持 `"cf-12345"` / `"mr-AABBCCDD"` / 纯数字自动判断。

### 3. 分页

| | CurseForge | Modrinth |
|--|-----------|----------|
| 方式 | `index` + `pageSize` | `offset` + `limit` |
| 上限 | `index + pageSize <= 10000` | 默认 10，最大 100 |
| 下一页 | 计算 `index + pageSize` | 计算 `offset + limit` |
| 总数 | `pagination.totalCount` | `total_hits`（搜索） |

**策略**：统一使用 `Offset` / `Limit`。CurseForge 适配层做 `index → offset` 的直接映射。

### 4. 哈希算法

| | CurseForge | Modrinth |
|--|-----------|----------|
| 可用 | Sha1, Md5 | Sha1, Sha512 |
| 哈希匹配 | `POST /v1/fingerprints`（模糊 + 精确） | `POST /v2/version_files/from_hashes` |
| 更新检测 | 无专用端点 | `POST /v2/version_file/{hash}/update` |

**策略**：`MatchFingerprints` 同时对接两个端点。CurseForge 借助指纹匹配 + 文件 ID 找最新文件模拟更新检测。

### 5. 状态枚举差异

**CompProjectStatus**（统一枚举）：

| Comp 值 | CurseForge (`CoreStatus`) | Modrinth 字符串 |
|---------|--------------------------|-----------------|
| `Approved` | 5=Approved, 6=Live | `approved` |
| `Draft` | 1=Draft | `draft` |
| `Rejected` | 4=Rejected | `rejected` |
| `Archived` | — | `archived` |
| `Unlisted` | — | `unlisted` |
| `Processing` | 2=Test, 3=PendingReview | `processing` |
| `Withheld` | — | `withheld` |
| `Scheduled` | — | `scheduled` |
| `Unknown` | 其他 | `unknown` |

**ModLoaderType**（统一枚举）：

| Comp 值 | CurseForge 值 | Modrinth 字符串 |
|---------|--------------|-----------------|
| `Any` | 0=Any | — |
| `Forge` | 1=Forge | `forge` |
| `Cauldron` | 2=Cauldron | — |
| `LiteLoader` | 3=LiteLoader | — |
| `Fabric` | 4=Fabric | `fabric` |
| `Quilt` | 5=Quilt | `quilt` |
| `NeoForge` | 6=NeoForge | `neoforge` |
| `Rift` | — | `rift` |
| `Data` | — | `data` |

### 6. 分类体系差异

**CurseForge**：
- 双层结构：Class（大类）→ Category（子类）
- 每个分类有 `id`、`gameId`、`slug`、`iconUrl`
- 通过 `GET /v1/categories?gameId=432`（Minecraft=432）获取

**Modrinth**：
- 扁平分类标签列表，与 Loader 分离
- 每个分类有 `name`、`icon`（SVG）、`project_type` 归属
- 通过 `GET /v2/tag/category` 获取

**策略**：统一模型 `CompCategory { Id, Name, Slug, IconUrl, ParentId }`。Modrinth 适配层将扁平分类映射为 ParentId=null 的根分类。

### 7. 下载机制差异

| | CurseForge | Modrinth |
|--|-----------|----------|
| 下载 URL 提供方式 | 文件对象含 `downloadUrl`，另有专用端点刷新 | 文件对象直接含 `url` |
| CDN 行为 | 直接返回 URL | CDN 文件自动分发 |
| 文件哈希 | `hashes[]` (Sha1/Md5) | `files[].hashes` (sha1/sha512) |

**策略**：优先使用文件对象自带的 URL。CurseForge 客户端可缓存 `downloadUrl` 并在 403 时调用刷新端点。

### 8. 限流差异

| | CurseForge | Modrinth |
|--|-----------|----------|
| 频率 | 未公开（API Key 机制未显示配额） | 300 req/min |
| Header | 未明确 | `X-Ratelimit-*` |
| 超限响应 | 429 | 429 |

**策略**：内置可配置的限流器，默认遵守 300 req/min。支持 Retry-After 响应处理。

---

## 架构建议

```
┌─────────────────────────────────────────────────────┐
│                    启动器应用                          │
├─────────────────────────────────────────────────────┤
│              ICompClient (统一接口)                    │
├──────────────────┬──────────────────┬────────────────┤
│ CurseForgeClient │ ModrinthClient   │ AggregateClient│
│ (ICompClient)    │ (ICompClient)     │ (路由/合并)     │
├──────────────────┴──────────────────┴────────────────┤
│                 HTTP 层 (IHttpClientFactory)          │
│    限流 / 重试 / 日志 / 认证注入 / User-Agent          │
├─────────────────────────────────────────────────────┤
│               CurseForge REST API                    │
│              Modrinth REST API                       │
└─────────────────────────────────────────────────────┘
```

### 扩展建议

- **插件化 Provider**：通过 DI 注册 `ICompClient` 的具体实现，启动器可同时持有两个客户端。
- **缓存层**：元数据（游戏版本、分类、加载器）应缓存，避免重复请求。
- **错误处理**：统一抛出自定义 `CompApiException`，携带 HTTP 状态码、Provider、原始错误消息。
- **User-Agent**：按 Modrinth 要求，在所有请求中注入 `启动器名/版本 (联系方式)`。

---

## 文件结构建议

```
Comp/
├── Models/
│   ├── CompProject.cs
│   ├── CompFile.cs
│   ├── CompCategory.cs
│   ├── CompGameVersion.cs
│   ├── CompLoader.cs
│   ├── CompSearchFilter.cs
│   ├── CompSearchResult.cs
│   └── Enums/
│       ├── CompProjectType.cs
│       ├── CompProjectStatus.cs
│       ├── ModLoaderType.cs
│       ├── HashAlgorithm.cs
│       ├── CompSortField.cs
│       └── SortOrder.cs
├── Abstractions/
│   ├── ICompClient.cs
│   └── ICompClientFactory.cs
├── Clients/
│   ├── CurseForgeClient.cs
│   ├── ModrinthClient.cs
│   └── AggregateClient.cs
├── Infrastructure/
│   ├── RateLimiter.cs
│   ├── RetryHandler.cs
│   ├── AuthHandler.cs
│   └── CompApiException.cs
└── Converters/
    ├── CurseForgeModelConverter.cs
    └── ModrinthModelConverter.cs
```
