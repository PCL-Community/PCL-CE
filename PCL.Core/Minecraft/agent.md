# Minecraft 目录

Minecraft 领域逻辑层，位于命名空间 `PCL.Core.Minecraft`。涵盖游戏核心操作、版本/文本处理、NBT、存档导入、服务器地址解析，以及下层的身份认证模型、Java 解析与扫描、启动、游戏档案与认证、资源项目与存档编辑/解析。

## 本目录模块

本目录为容器层，直接包含若干跨越多个子系统的通用工具与模型类，本身不依赖子目录的实现，但被 `Launch`、`Saves`、`Profile` 等子目录广泛使用。

- **GameCore.cs**：游戏核心 Jar 操作类 `GameCore`。
  - 职责：向游戏核心 Jar 注入 Mod 或其他补丁文件，并在注入后清理签名目录以避免 Oracle JDK 加载时因签名校验失败而无法启动。
  - 关键成员：`GameCore(string corePath)` 构造函数（文件不存在时抛 `FileNotFoundException`）；`AddToCore(string jarPath)` 以 `ZipArchiveMode.Update` 打开核心 Jar，遍历源 Jar/Zip 的条目，仅复制命中过滤规则的条目（源为 `.jar` 时过滤串为 `""`，否则为 `"MINECRAFT-JAR"`），最后删除 `META-INF` 目录。
  - 角色：启动前的核心 Jar 修补；输入输出均为磁盘上的 Jar/Zip 文件。
- **MavenArtifact.cs**：Maven 构件坐标解析类 `MavenArtifact`。
  - 职责：将 `groupId:artifactId:version`（以及带 packaging、classifier 的 4~5 段）坐标转换为仓库相对路径或 URI。
  - 关键成员：主构造函数 `MavenArtifact(string mavenId)`；`Resolve(string uriOrPath)` 拼接基址与 `_GetMavenPath` 计算出的路径；`_GetMavenPath` 按坐标段数（3/4/5）生成 `/g/a/a-v.jar` 等路径；`_IsCommonPackaging` 识别 jar/zip/pom。
  - 角色：供依赖下载与版本清单解析定位构件地址。
- **McFormatter.cs**：MC 文本与 Wiki 链接格式化静态类 `McFormatter`（位于 `PCL.Core.Minecraft`）。
  - 职责：根据当前 UI 语言构造 Minecraft Wiki 基础 URL 与版本页后缀，并把历史/特殊版本号格式化为 Wiki 页面名。
  - 关键成员：`GetWikiBaseUrl()`（依据 `_WikiPrefixMap` 与 `LocalizationService.CurrentLanguage.Code`）；`GetWikiUrlSuffix(string gameVersion)`；`FormatVersion(string gameVersion)`（大量 `switch`/前缀映射，处理 Classic/Indev/Infdev/Alpha/Beta/Combat Test/愚人节版本等）。
  - 角色：下载或帮助页中链接到对应版本 Wiki；依赖 `LocalizationService`、`Lang`。
- **McVersionClassifier.cs**：MC 版本分类器静态类 `McVersionClassifier`（注意其命名空间为 `PCL`，而非 `Minecraft`）。
  - 职责：判定版本类型（Release / Snapshot / BeforeRelease / AprilFools）并生成本地化类型键与愚人节版本描述。
  - 关键成员：枚举 `McVersionCategory`；`ClassifyVersion(JsonObject version)` 读取 `type`/`id` 分类；`_TryMarkAprilFoolsVersion` 通过 ID 模式或发布日期（4 月 1 日 UTC+2）识别愚人节版本；`GetCategoryKey`、`GetReleaseTime`、`GetMcFoolName`/`GetMcFoolNameKey`。
  - 角色：下载界面版本分组与展示；依赖 `JsonCompat`、`Lang`、`LocalizationService`。
- **NbtFileHandler.cs**：NBT 文件异步读写静态类 `NbtFileHandler`（基于 `fNbt`）。
  - 职责：在 `CancellationToken` 下异步读取/写入 NBT 文件中的指定标签。
  - 关键成员：`ReadTagInNbtFileAsync<T>(filePath, tagName, ct)` 读取根标签下指定类型标签，文件/标签缺失或异常时返回 `null` 并记录日志；`WriteTagInNbtFileAsync(nbtTag, filePath, compression, ct)` 若文件不存在则创建目录与文件。
  - 角色：被 `Saves` 等子目录复用的底层 NBT I/O 工具。
- **SaveImportHelper.cs**：存档导入辅助静态类 `SaveImportHelper`。
  - 职责：从解压后的目录中定位真正的存档根目录（包含 `level.dat`）。
  - 关键成员：`GetSaveRootDirectory(string extractedDirectory)`，若当前目录直接含 `level.dat` 直接返回，否则在只有唯一子目录且子目录含 `level.dat` 时返回该子目录；否则返回 `null`。
  - 角色：导入外部存档（如压缩包）时确定存档边界。
- **ServerAddressResolver.cs**：服务器地址解析与可达性校验静态类 `ServerAddressResolver`。
  - 职责：解析 `host:port`/IPv6 中括号/带 scheme 的地址，查询 SRV（`_minecraft._tcp.`），并通过 Happy Eyeballs（先 IPv6 后 IPv4，间隔 250ms）与 TCP 连接（超时 2.5s）验证可达性，输出「主机/解析 IP/端口」。
  - 关键成员：`ResolvedServerAddress` 只读记录结构；`GetResolvedServerAddressAsync`；私有 `_ParseHostAndPort`、`_QuerySrvOrderedAsync`（RFC 2782 按 priority 升序、同组按 weight 加权随机）、`_ResolveReachableAsync`、`_ConnectAnyAsync`/`_ConnectOneAsync`。
  - 角色：多人联机前对服务器地址做规范化与连通性预判；依赖 `DnsQuery`、`RandomUtils`、`LogWrapper`、`PEHeaderReader` 所在工具链。

## 直接代码要点

- **运行流程（以 `ServerAddressResolver` 为例）**：规范化输入 → 解析 host/port → 显式端口直接解析 IP 并尝试连接 → 纯 IP 用默认端口 → 域名先查 SRV 再按记录尝试，全部不可达则回退域名+默认端口；连接采用 Happy Eyeballs 分组并行、首个成功即取消其余。
- **输入与输出规范**：地址字符串可为 `host`、`host:port`、`[IPv6]:port`、`scheme://host`；输出 `ResolvedServerAddress(Host, Ip, Port)`，其中 `Ip` 可能为空（仅当解析完全失败）。
- **使用约束与注意事项**：空地址抛 `ArgumentException`，非法端口/IPv6 抛 `FormatException`；DNS/连接异常被吞并回退到「域名+默认端口」，不会抛出网络异常；`McVersionClassifier` 会就地修改传入 `JsonObject` 的 `type`/`id`（副作用，调用方需注意）。

## 子目录导航

- [IdentityModel](./IdentityModel/agent.md)：身份认证模型（OAuth/Yggdrasil/JWT/OpenID/PKCE）。
- [Java](./Java/agent.md)：Java 解析、扫描与用户偏好。
- [Launch](./Launch/agent.md)：启动相关工具。
- [Profile](./Profile/agent.md)：游戏档案与认证。
- [ResourceProject](./ResourceProject/agent.md)：资源项目（CurseForge/Modrinth）。
- [Saves](./Saves/agent.md)：存档编辑与解析。
- [Yggdrasil](./Yggdrasil/agent.md)：Yggdrasil API 配置。
