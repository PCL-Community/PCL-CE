# App/Configuration 目录

全局配置子系统，位于命名空间 `PCL.Core.App.Configuration`。以 `[ConfigItem]` 特性声明配置项，由源生成器在 `ConfigService` 上生成强类型读写属性；支持共享/本地两种配置来源、配置变更事件、观察者、值缓存与版本迁移。底层文件读写由 `Storage` 子目录提供。

## 本目录模块

本目录实现全局配置子系统，是 `App/Config.cs`、`App/States.cs` 中 `[ConfigItem]`/`[ConfigGroup]` partial 类的运行时支撑。核心思路：以特性声明配置项 → 源生成器生成强类型访问器 → `ConfigService` 在生命周期中加载/保存 → `ConfigItem<T>` 处理读写、缓存与事件。底层文件读写由 `Storage` 子目录提供。

- **ArgConfig.cs**：参数化配置包装类型 `ArgConfig<TValue> : ParameterizedProperty<object, TValue>`。用于"实例独立配置"（`Config.InstanceConfigGroup` 等），其 getter/setter 由外部 lambda 提供，从而把值绑定到具体实例上下文参数（如实例路径）。自身不含逻辑，仅承载委托。

- **Attributes.cs**：配置相关特性（均在 `PCL.Core.App.Configuration` 命名空间）。
  - `ConfigItemAttribute<TValue>(key, defaultValue, source)`：标记 partial 属性为配置项，自动生成访问器。
  - `AnyConfigItemAttribute<TValue>(key, source)`：同上，但默认值通过无参构造器获取（用于无默认字面量的复杂类型）。
  - `ConfigGroupAttribute(name, source)`：标记 partial 类为配置组，自动实现 `IConfigScope` 并生成作用域检查。
  - `RegisterConfigEventAttribute`：标记 `ConfigEventRegistry` 静态属性以注册配置事件。

- **ConfigEvent.cs**：`[Flags] enum ConfigEvent`，定义配置事件类型（`Init`/`Get`/`Set`/`Reset`/`CheckDefault` 及组合 `Read`/`Update`/`Changed`/`All`）。用于 `ConfigObserver` 指定监听哪些操作。

- **ConfigEventArgs.cs**：`record ConfigEventArgs(Item, Event, Argument, OldValue, NewValue)` 配置事件参数。额外提供 `NewValueReplacement`（预览阶段替换值）与 `Cancelled`（预览阶段取消事件）及便捷 `Value` 属性，仅在 `ConfigObserver.IsPreview` 为 `true` 时生效。

- **ConfigEventRegistry.cs**：`class ConfigEventRegistry(scope, handler, trigger, isPreview)`。把一组 `IConfigScope` 与一个处理器绑定，作为 `[RegisterConfigEvent]` 特性的目标类型；提供 `ToObserver()` 转为 `ConfigObserver`。同时有接受单个 `IConfigScope` 的便捷构造。

- **ConfigEventHandler.cs**：`delegate void ConfigEventHandler(ConfigEventArgs e)`，配置事件处理委托。

- **ConfigItem.cs**：配置项核心模型 `class ConfigItem<TValue> : IConfigScope, ConfigItem`（非泛型 `interface ConfigItem` 亦在本文件定义，用于去泛型化互操作）。
  - 关键成员：`Key`/`Source`/`Type`/`DefaultValue`；`EnableCache`（默认 `true`，禁用会影响内容变更监听）、`_valueCache`（`ConfigValueCache<TValue>`）。
  - 关键方法：`GetValue`/`GetValueNoType`（读，含缓存与事件触发，预览事件可取消或替换返回值）、`SetValue`/`SetValueNoType`（写，兼容字符串转换）、`SetDefaultValue`、`Reset`、`IsDefault`、`Observe`/`Unobserve`（注册预览/普通观察者）。
  - 事件机制：`_TriggerEvent` 按 `(int)observer.Event & (int)trigger` 位运算筛选观察者，预览观察者（`IsPreview`）可取消或替换值，普通观察者无法传递取消/替换状态。
  - 角色：每个 `[ConfigItem]` 属性背后就是一个 `ConfigItem<T>`，是读写、缓存、事件的总枢纽。

- **ConfigMigration.cs**：配置文件迁移工具 `class ConfigMigration` + `delegate ConfigMigrationHandler`。
  - 字段：`From`/`To`（路径）、`Priority`（权重，越大越优先）、`OnMigration`（实现）。
  - `Migrate(target, migrations)`：用 BFS 构图找到从存在的旧文件到 `target` 的最短边序列，再在最短路径子图上按累计 `Priority` 最大选择迁移方案并执行；用于旧版 `setup.ini`/`config.json` 向新版 `config.v1.yml`/`config.v1.json` 迁移。

- **ConfigObserver.cs**：`record ConfigObserver(Event, Handler, IsPreview=false)`，配置事件观察器，描述"监听哪些事件 + 处理委托 + 是否预览"。

- **ConfigService.cs**：全局配置服务核心 `public sealed partial class ConfigService`（`[LifecycleService(LifecycleState.Loading, Priority=1919810)]`、`[LifecycleScope("config","配置")]`）。
  - 关键成员：`_Items`（所有配置项字典）、`KeySet`、`SharedVersion`/`LocalVersion`（文件版本，自身也是配置项）、`SharedConfigPath`（`Paths.SharedData/config.v1.json`）、`LocalConfigPath`（`Paths.Data/config.v1.yml`）。
  - 关键方法：`GetConfigItem`/`TryGetConfigItem`/`TrySetValue`（按 key 读写）、`RegisterObserver`（向作用域批量注册观察器）、`GetProvider(ConfigSource)`（返回对应 `IConfigProvider`）、`_InitializeProviders`（初始化 4 个 provider：共享、共享加密、本地、实例动态缓存，含旧文件迁移）、`_Start`/`_Stop`（`[LifecycleStart]`/`[LifecycleStop]`，加载→初始化项→初始化 provider→初始化观察者→触发 Init 事件；停止时保存并释放 provider）。
  - 角色：整个配置子系统的入口与生命周期锚点，`Config`/`States` 的 partial 属性最终都委托给这里。

- **ConfigSource.cs**：`enum ConfigSource`（`Shared` 共享 / `SharedEncrypt` 共享加密 / `Local` 本地 / `GameInstance` 游戏实例），决定配置值落到哪个 provider/文件。

- **ConfigValueCache.cs**：`struct ConfigValueCache<TValue>` 配置值缓存。支持"无上下文"单值与"按 `argument` 上下文"的 `ConcurrentDictionary` 两种缓存，`TryRead`/`Write`/`Invalidate`/`InvalidateAll`，用于 `ConfigItem` 去重相同值并监听 `INotifyPropertyChanged`/`INotifyCollectionChanged` 内容变更。

- **IConfigProvider.cs**：`interface IConfigProvider`。配置底层读写契约：`GetValue<T>`/`SetValue<T>`/`Delete`/`Exists`，均以 `key` + `argument`（上下文，如实例路径）为参数。`ConfigService.GetProvider` 返回的实现位于 `Storage` 子目录。

- **IConfigScope.cs**：`interface IConfigScope`。配置作用域契约：`CheckScope(keys)`（返回本作用域包含的键）、`Reset`/`IsDefault`（按 argument 重置/检查），供 `ConfigGroup` 与 `ConfigItem` 复用，便于"一组配置项"统一注册观察者或整体重置。

## 本目录直接代码：运行流程与约束

- **运行流程**：应用启动时 `ConfigService._Start`（生命周期 Loading 阶段）依次：初始化所有 `ConfigItem`（源生成器收集）→ 初始化 4 类 `IConfigProvider`（含旧文件迁移）→ 初始化观察者 → 对每个项触发 `Init` 事件；停止时 `_Stop` 保存并释放 provider。读写配置项时，`ConfigItem<T>.Get/SetValue` 经对应 `IConfigProvider` 落到 `Storage` 子目录的文件。
- **输入输出规范**：所有读写以 `key`（字符串）+ `argument`（上下文对象，如实例目录路径）为参数；`ConfigItem.SetValueNoType` 兼容字符串值转换；配置来源由 `ConfigSource` 决定落盘位置（共享/加密/本地/实例）。
- **使用约束与注意事项**：禁用 `ConfigItem.EnableCache` 会破坏内容变更自动监听，仅在确有需要时关闭；预览观察器（`IsPreview=true`）可取消事件或替换值，普通观察器不可；新增配置项必须带 `[ConfigItem]`/`[ConfigGroup]` 特性并由源生成器生成 partial 实现；`ConfigMigration` 用于历史格式迁移，路径与优先级需谨慎设置。

## 子目录导航

- [Storage](./Storage/agent.md)：配置文件的底层存储与键值文件提供器。
