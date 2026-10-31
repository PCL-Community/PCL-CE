# App 目录

应用基础设施层，位于命名空间 `PCL.Core.App`。提供启动器运行所需的全局配置、生命周期与依赖注入、事件总线、本地化、区域策略、任务调度、数据库与一系列应用级服务（启动、单实例、更新、遥测、RPC、主窗口等），并包含命令行解析入口。多数核心服务通过 `[LifecycleService]` 特性由源生成器（`App.IoC`）在应用生命周期中自动注册与启停。

## 本目录模块

本目录为应用基础设施的容器层，直接包含 7 个 `.cs` 文件，定义全局只读元数据、路径、密钥、配置入口与状态。它们均为 `PCL.Core.App` 命名空间下的静态/记录类型，被 `App` 下的子目录（`Configuration`、`Essentials` 等）及启动器主程序消费。

- **Basics.cs**：基础工具集（静态类 `Basics`）。职责：暴露启动器全局只读元数据与运行环境信息。
  - 关键成员：`Metadata`（`MetadataModel`，从嵌入资源 `PCL.metadata.json` 反序列化）、`VersionName`/`VersionCode`/`VersionBranch`、`IsAprilFool`、`CurrentProcess`/`CurrentProcessId`/`ExecutablePath`/`ExecutableDirectory`/`ExecutableName`/`CommandLineArguments`/`CurrentDirectory` 等进程与路径常量。
  - 关键方法：`RunInNewThread(Action, name, priority)`（后台线程运行委托并吞掉异常/中断）、`GetParentPath`/`GetParentPathOrEmpty`/`GetParentPathOrDefault`（父路径计算）、`OpenPath`（用 Shell 打开路径，对无关联文件回退到记事本）、`GetResourceStream`/`GetAppImagePath`（读取 `pack://` 打包资源）。
  - 角色：几乎被全项目引用的全局只读常量来源；路径相关方法提醒调用方优先使用 `ExecutableDirectory` 而非 `CurrentDirectory`。

- **Config.cs**：全局配置入口（`public static partial class Config`）。通过 `[ConfigGroup]` 嵌套 partial 类 + `[ConfigItem<T>]` 源生成器生成强类型属性，聚合所有全局配置项。
  - 包含的配置组：系统（`SystemConfigGroup`：硬件加速、遥测、最大日志、档案等）、网络/代理（`NetworkConfigGroup`）、调试（`DebugConfigGroup`）、下载（`DownloadConfigGroup` 含第三方资源 `CompConfigGroup`）、工具（`ToolConfigGroup`）、更新（`UpdateConfigGroup`）、联机（`LinkConfigGroup`）、个性化（`PreferenceConfigGroup` 含本地化/主题/背景/模糊/主页/音乐/功能隐藏等大量子组）、启动（`LaunchConfigGroup` 内存/ JVM 参数/窗口等）、实例独立（`InstanceConfigGroup`/`InstanceAuthConfigGroup`，值类型为 `ArgConfig<T>`）。
  - 角色：配置子系统的"门面"，所有业务代码通过 `Config.XXX` 读取/写入配置；实际存储由 `App/Configuration` 子目录的 `ConfigService` 提供。

- **ConfigEnums.cs**：配置相关枚举集合（均在 `PCL.Core.App` 命名空间）。包括 `LinkProtocolPreference`（TCP/UDP）、`DownloadHttpMode`（Auto/Http11/Http2）、`ColorMode`（Light/Dark/System）、`ColorTheme`（SkyBlue/CatBlue/DeathBlue/HmclBlue）、`UpdateChannel`、`GameWindowSizeMode`、`GameProcessPriority`、`LauncherVisibility`、`JvmPreferredIpStack`、`LinkRelayBehavior`、`LauncherAutoUpdateBehavior`、`LauncherTitleType`。这些枚举被 `Config.cs` 的配置项作为值类型引用。

- **Metadata.cs**：启动器元数据模型（`PCL.Core.App` 命名空间下的记录类型）。
  - `LauncherVersionModel`：版本型号，`BaseVersion`/`Suffix`/`Code`/`UpstreamVersion`；派生出 `BaseName`/`BranchName`/`BranchCode`/`StandardVersion`/`Commit`/`CommitDigest`（编译期分支信息 + CI 提交哈希）。
  - `ThirdPartyLicenseModel`：第三方许可信息（名称、说明、网站、许可证 URI）。
  - `MetadataModel`：聚合 `Name` + `Version` + `Licenses[]`，即 `Basics.Metadata` 的反序列化目标。

- **Paths.cs**：全局路径与目录管理（`public static class Paths`）。
  - 属性：`DefaultDirectory`（= `Basics.ExecutableDirectory`）、`Data`（实例数据目录）、`SharedData`/`OldSharedData`（共享数据，`OldSharedData` 仅用于迁移）、`SharedLocalData`（大文件/可再下载）、`Temp`。
  - 方法：`GetSpecialPath(Special, relative)`（特殊文件夹相对路径）；静态构造函数按 `DEBUG` 选择目录名（`PCLCE`/`PCLCE_Debug`），组合路径并创建目录，DEBUG 下还读取 `PCL_PATH*` 环境变量。
  - 角色：所有文件系统根路径的唯一定义点，其他模块通过 `Paths.Data` 等访问。

- **Secrets.cs**：密钥/敏感配置常量（`public static class Secrets`）。所有值通过 `EnvironmentInterop.GetSecret(key, readEnvDebugOnly: true)` 从环境变量/密钥源读取，并在调试构建外才生效，提供 `ReplaceNullOrEmpty` 兜底。
  - 关键成员：`MSOAuthClientId`（微软 OAuth）、`CurseForgeAPIKey`、`TelemetryKey`、`NatayarkClientId`/`NatayarkClientSecret`（Natayark ID OAuth）、`LinkServers`（联机根服务器，按 `|` 分割）、`CommitHash`（Git SHA）。
  - 角色：运行时密钥的唯一来源，避免把密钥硬编码进代码。

- **States.cs**：全局应用状态（`public static partial class States`）。与 `Config` 类似以 `[ConfigGroup]` 嵌套 partial 类声明，但语义上是"运行时状态/缓存"，同样由 `Configuration` 子系统持久化。
  - 包含的状态组：根级 `Identifier`/`CustomVariables`；`HintStatesGroup`（各类一次性提示标记）、`GameStatesGroup`（Java 列表版本、当前实例/文件夹、Java 列表、收藏资源等）、`UiStatesGroup`（主页 URL、窗口宽高）、`SystemStatesGroup`（UUID、启动次数、EULA、各版本号等）、`ToolStatesGroup`（下载目录/UA、最新版本）、`LinkStatesGroup`（联机 EULA、公告缓存、Naid 令牌等）、`InstanceStatesGroup`（实例独立状态：启动次数、星标、各加载器版本、Vanilla 版本等，多为 `ArgConfig<T>`）。
  - 角色：跨会话/跨实例保存的用户状态与统计，区别于 `Config` 的偏好设置。

## 本目录直接代码：运行流程与约束

- **运行流程**：`Paths` 在静态构造时建好所有根目录；`Basics.Metadata` 在首次访问时从嵌入资源反序列化；`Config`/`States` 的配置项属性由 `Configuration` 子系统的源生成器在编译期生成，运行时由 `ConfigService` 加载/保存。
- **输入输出规范**：所有路径应使用 `System.IO.Path` 相关 API 组合，禁止字符串手工拼接（见 `Paths` 与 `Basics` 的注释）；密钥仅经 `Secrets` 读取，不在代码内明文出现。
- **使用约束与注意事项**：`Config` 与 `States` 的 partial 类由源生成器补全，直接新增配置项需加 `[ConfigItem]`/`[ConfigGroup]` 特性并在对应类型内声明 partial 属性；`Paths.OldSharedData` 仅用于迁移，禁止在新逻辑中使用；`Secrets` 在 DEBUG 下 `readEnvDebugOnly:true` 意味着正式构建读取不到本地环境变量。

## 子目录导航

- [Cli](./Cli/agent.md)：命令行参数解析与定义。
- [Configuration](./Configuration/agent.md)：全局配置服务与配置存储。
- [Database](./Database/agent.md)：数据库服务。
- [Essentials](./Essentials/agent.md)：应用级核心服务（启动、单实例、更新、遥测、RPC、主窗口等）。
- [EventBus](./EventBus/agent.md)：事件总线发布/订阅服务。
- [IoC](./IoC/agent.md)：依赖注入与生命周期管理。
- [Localization](./Localization/agent.md)：本地化与语言服务。
- [Region](./Region/agent.md)：区域策略。
- [Tasks](./Tasks/agent.md)：任务调度与任务模型。
- [Tools](./Tools/agent.md)：依赖检查工具服务。
