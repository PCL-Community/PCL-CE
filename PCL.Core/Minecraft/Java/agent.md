# Minecraft/Java 目录

Java 管理子系统，位于命名空间 `PCL.Core.Minecraft.Java`。负责发现、解析与存储本机 Java 安装，并按用户偏好选择启动游戏所用的 Java。

## 本目录模块

本目录为 Java 子系统的容器层，定义 Java 的模型、常量与核心管理器，具体的可执行文件解析、安装扫描、用户偏好分别在 `Parser`、`Scanner`、`UserPreference` 子目录中实现，并由 `JavaService` 在生命周期中装配。

- **JavaBrandType.cs**：Java 品牌枚举 `JavaBrandType`（EclipseTemurin、Liberica、Zulu、Corretto、Microsoft、IBMSemeru、Oracle、Dragonwell、TencentKona、OpenJDK、GraalVmCommunity、JetBrains、Unknown），用于安装排序与展示时的品牌优选。
- **JavaConsts.cs**：Java 相关常量类 `JavaConsts`。
  - 关键成员：`ExcludeFolderNames`（扫描时跳过的目录，如 `javapath`、`netease`）；`MostPossibleKeywords`/`PossibleKeywords`/`AllKeywords`：扫描路径命中关键词表，供 `Scanner` 缩小候选目录范围。
  - 角色：被 `Scanner` 子目录用于快速剪枝无关目录。
- **JavaSource.cs**：Java 来源枚举 `JavaSource`（AutoScanned / AutoInstalled / ManualAdded），记录一条 Java 记录是如何进入管理的，用于持久化与排序权重。
- **JavaEntry.cs**：Java 条目记录 `JavaEntry`（sealed class）。
  - 关键属性：`Installation`（`JavaInstallation`，必填）、`IsEnabled`、`Source`。`ToString` 显示启用标记与安装信息。
  - 角色：运行时内存中的 Java 项，是 `JavaManager` 字典的 value。
- **JavaInstallation.cs**：Java 安装信息记录 `JavaInstallation`（sealed record）。
  - 关键属性：`JavaFolder`、`Version`、`Brand`、`Architecture`（MachineType）、`Is64Bit`、`IsJre`；派生属性 `JavaExePath`、`JavawExePath`、`MajorVersion`（将 1.8 映射为 8）、`IsStillAvailable`（检查 `java.exe` 是否仍存在）。
  - 角色：一次 Java 安装的物理描述，由 `Parser` 子目录产出。
- **JavaStorageItem.cs**：Java 持久化存储项 `JavaStorageItem`，对应配置中序列化的 `Path`/`IsEnable`/`Source`，是 `JavaManager` 配置读写的 DTO。
- **JavaManager.cs**：Java 管理器 `JavaManager`（核心协调者）。
  - 职责：在内存中维护 `Dictionary<string, JavaEntry>`，协调扫描、解析、增删查与按版本范围选取合适 Java。
  - 关键成员：构造函数注入 `IJavaParser` 与 `IJavaScanner[]`；`ReadConfig`/`SaveConfig`（与 `States.Game.JavaList` 互转 `JavaStorageItem[]`）；`ScanJavaAsync`（带 13s 最小间隔与 7s 取锁超时，并行调用各 scanner 后用 parser 解析去重）；`GetSortedJavaList`（按版本降序、品牌升序）；`SelectSuitableJavaAsync(minVersion, maxVersion)`（无记录时先扫描，按主版本→JDK 优先→品牌→小版本排序选取）；`AddOrGet`/`Get`/`CheckAllAvailability`/`Exist*`；静态 `NormalizeVersion`（1.x → x.0.0）与 `IsVersionSuitable`。
  - 角色：Java 子系统对外的状态与选择入口；依赖 `Parser`、`Scanner` 子目录与 `JsonCompat`、`LogWrapper`、`RandomUtils` 等。
- **JavaService.cs**：Java 生命周期服务 `JavaService`（`[LifecycleService(LifecycleState.Loaded)]`、`[LifecycleScope("java", "Java 管理")]`、partial）。
  - 职责：在应用加载时装配并启动 `JavaManager`（`_StartAsync` 内 new 出 `PeHeaderParser` 与多个 scanner：`RegistryJavaScanner`、`DefaultPathsScanner`、`PathEnvironmentScanner`、`MicrosoftStoreJavaScanner`、`WhereCommandScanner`，随后 `ReadConfig` 与 `ScanJavaAsync`），停止时 `SaveConfig`。
  - 角色：将 Java 管理接入应用生命周期，对外暴露静态 `JavaManager`。

## 直接代码要点

- **运行流程**：`JavaService._StartAsync` → 构造 `JavaManager`（注入解析器+扫描器）→ `ReadConfig`（反序列化已有记录）→ `ScanJavaAsync`（并行扫描目录、解析、过滤排除目录、按默认策略决定是否启用）→ 记录日志；运行时通过 `SelectSuitableJavaAsync` 按游戏版本区间挑选。
- **输入与输出规范**：配置以 `JavaStorageItem[]` 的 JSON 存入 `States.Game.JavaList`；`SelectSuitableJavaAsync` 输入为版本闭区间，输出为按优先级排序的 `JavaEntry[]`。
- **使用约束与注意事项**：扫描有 13s 最小间隔与 7s 取锁超时，频繁调用会被跳过；`JavaManager` 内部对 `_javaEntrys` 加锁保护，但 `ScanJavaAsync` 在持锁前仍会先判断间隔；解析失败或文件不存在的条目会被静默跳过；`MajorVersion` 对 1.8 系列统一映射为 8，比较版本前应使用 `NormalizeVersion`。

## 子目录导航

- [Parser](./Parser/agent.md)：Java 可执行文件解析（判定版本/位数）。
- [Scanner](./Scanner/agent.md)：Java 安装扫描器。
- [UserPreference](./UserPreference/agent.md)：Java 用户偏好。
