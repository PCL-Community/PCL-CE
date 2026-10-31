# Modules/Minecraft

Minecraft 业务核心模块，命名空间 `PCL`。覆盖实例管理、版本/组件、下载、启动、Java、皮肤与日志等，并包含崩溃分析子系统。

## 本目录直接文件（按职责分组）

- **实例管理**：`McInstance`（实例对象，`Check`/`Load`/`GetDefaultDescription`/`Equals`）、`McInstanceInfo`（`FormatVersion`/`IsFormatFit`/`VersionToDrop`/`GetVanillaNameDisplay`/`DropToVersion`）、`McInstanceCardType`、`McInstanceState`（实例信息与状态）、`ModInstanceList`（实例列表增删改查）、`ProfileUi`（档案 UI 辅助：设备码登录、错误处理、档案增删改、皮肤等）。
- **组件/版本**：`ModComp`（组件解析与管理，含 SQLite 元数据）、`ModCompDependency`、`ModLocalComp`（本地组件）、`ModModpack`（整合包）、`ModLibrary`（库文件）、`ModAssets`（资源索引）、`ModFolder`（目录结构）、`ModJarInJar*`（`JarInJar` 嵌套依赖相关）、`ModLoaderType`（加载器类型枚举）。
- **下载/启动/运行**：`ModDownload`（客户端与组件下载）、`ModLaunch`（启动游戏，组装 JVM 参数与进程）、`ModJava`（Java 运行时探测与管理）、`ModWatcher`（文件监听）、`ModStyle`（样式）。
- **版本比较/约束**：`McVersionComparer`（`CompareVersionGe`/`CompareVersion`/`Compare`）、`MavenComparableVersion`（Maven 版本序，`Normalize`/`Compare`，含 `NumericItem`/`StringItem`/`CombinationItem`/`ListItem`）、`McConstraintMatcher`（`Match`/`Satisfies`/`StripV`/`CompareMavenVersions`/`CompareFabricVersions`/`HasComparableBound`/`ContainsVersionToken`）。
- **辅助**：`ModSkin`（皮肤处理）、`McLogFilter`（`FilterAccessToken`/`FilterUserName` 脱敏）、`RestrictedFeaturePolicy`（`Assess`/`Check`/`ThrowIfRestricted` 受限功能策略）。

## 直接代码要点

- **运行流程**：`ModInstanceList` 维护实例集合 → `McInstance.Load` 读取实例元数据并组装 `McInstanceInfo` → `ModComp`/`ModLocalComp` 解析组件与依赖 → `ModLaunch` 依据 Java/组件/参数启动进程 → `ModDownload` 负责缺失资源下载。
- **输入输出规范**：输入为实例目录、版本清单、组件/依赖描述；输出为实例对象、启动参数/进程、下载任务；`ModLaunch` 需要 JVM 参数与 classpath。
- **使用约束与注意事项**：实例目录结构需与 `ModFolder` 约定一致；组件元数据存于 SQLite，注意并发与迁移；启动前需完成 Java 探测与文件校验；`McLogFilter` 必须在日志落盘前脱敏；受限功能由 `RestrictedFeaturePolicy` 统一拦截。

## 子目录导航

- [CrashAnalysis](./CrashAnalysis/agent.md)：启动崩溃分析子系统（收集→检测→导出→展示）。
