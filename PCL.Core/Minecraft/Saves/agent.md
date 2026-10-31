# Minecraft/Saves 目录

存档子系统，位于命名空间 `PCL.Core.Minecraft.Saves`。负责 Minecraft 存档的识别、解析与编辑，并按游戏数据版本选择对应的解析器/编辑器。包含损坏/缺失异常处理。

## 本目录模块

- **`SaveInfo.cs`** — 存档信息模型 `SaveInfo`：描述存档名称、路径、游戏版本、数据版本等元数据。
- **`SaveManager.cs`** — 存档管理器 `SaveManager`（核心）：`ScanSaveFoldersAsync` 扫描存档目录、`LoadSaveAsync`/`LoadSavesAsync` 加载单个/多个存档、`ApplyChangesAsync` 应用编辑变更并写回。
- **`DataVersionBoundaries.cs`** — 数据版本边界常量 `DataVersionBoundaries`：定义各解析/编辑策略适用的 DataVersion 区间。
- **`NBTEnums.cs`** — NBT 相关枚举：`Difficulty`（难度）、`GameMode`（游戏模式）、`SaveFormatVersion`（存档格式版本）。

## 直接代码要点

- **运行流程**：`ScanSaveFoldersAsync` 枚举存档目录 → `LoadSaveAsync` 读取 `level.dat` 并交由 `Parsing` 按 DataVersion 解析为 `SaveInfo` → 用户编辑产生变更 → `ApplyChangesAsync` 经 `Editing` 选择编辑器写回。
- **输入输出规范**：输入为存档目录/文件路径；输出为 `SaveInfo` 与变更结果；异步方法支持取消。
- **使用约束与注意事项**：必须先解析再编辑；写回需保证原子性（避免损坏 `level.dat`）；损坏/缺失由 `Exceptions` 表达；不同 DataVersion 的字段语义不同（如难度字节 vs `difficulty_settings`）。

## 子目录导航

- [Editing](./Editing/agent.md)：存档编辑。
- [Exceptions](./Exceptions/agent.md)：存档异常。
- [Parsing](./Parsing/agent.md)：存档解析。
