# Minecraft/Saves/Parsing 目录

存档解析子系统，位于命名空间 `PCL.Core.Minecraft.Saves.Parsing`。定义存档解析抽象，并按游戏数据版本分派到对应解析器。

## 本目录模块

- **`ISaveParser.cs`** — 存档解析器接口 `ISaveParser`：定义把 `level.dat`/NBT 解析为 `SaveInfo` 的契约。
- **`SaveParserFactory.cs`** — 解析器工厂 `SaveParserFactory`：`Resolve` 依据 DataVersion 选择并组合解析器（高版本优先、逐级回退）。

## 直接代码要点

- **运行流程**：读取存档 DataVersion → `SaveParserFactory.Resolve` 匹配适用解析器 → 解析 NBT 为 `SaveInfo` → 返回结果（字段缺失时按版本默认值兜底）。
- **输入输出规范**：输入为存档路径/DataVersion；输出为 `SaveInfo`；解析失败由 `Exceptions` 表达。
- **使用约束与注意事项**：DataVersion 边界由 `DataVersionBoundaries` 定义，跨版本字段可能缺失；解析器需对损坏 NBT 容错；工厂结果不应缓存可变状态。

## 子目录导航

- [Internal](./Internal/agent.md)：各版本区间的解析器实现。
