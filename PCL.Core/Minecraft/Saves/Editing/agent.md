# Minecraft/Saves/Editing 目录

存档编辑子系统，位于命名空间 `PCL.Core.Minecraft.Saves.Editing`。提供对存档内容的编辑抽象与变更收集。

## 本目录模块

- **`Editable.cs`** — 可编辑项 `Editable`：包装可修改并跟踪的存档字段，提供 `GetValueOrDefault`、`TryGetValue`、`ToString`，便于以统一方式读写与展示。
- **`ISaveEditor.cs`** — 存档编辑器接口 `ISaveEditor`：定义按 DataVersion 应用变更的契约（由 `Internal` 中各版本实现）。
- **`SaveChanges.cs`** — 存档变更集 `SaveChanges`：记录一次编辑产生的修改，供 `SaveManager.ApplyChangesAsync` 消费。

## 直接代码要点

- **运行流程**：读取 `SaveInfo` → 构造 `SaveChanges` 并填充 `Editable` → `ISaveEditor` 按 DataVersion 将变更写入 NBT → 原子写回存档。
- **输入输出规范**：输入为 `SaveChanges`（字段→新值）与目标存档；输出为写回后的存档文件；`Editable` 提供容错读取。
- **使用约束与注意事项**：必须先解析再编辑；不同版本字段结构不同（难度、`allowCommands`、`locked` 等），由 `Internal` 分版本处理；写回需保证原子性，避免半写损坏。

## 子目录导航

- [Internal](./Internal/agent.md)：各版本区间的编辑器实现。
