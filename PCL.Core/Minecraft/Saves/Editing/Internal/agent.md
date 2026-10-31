# Minecraft/Saves/Editing/Internal 目录

存档编辑器内部实现，位于命名空间 `PCL.Core.Minecraft.Saves.Editing.Internal`。两个编辑器均实现 `ISaveEditor` 接口，**只操作内存中的 `NbtCompound`（level.dat 的 Data 标签）**，文件 IO 由上层 `SaveManager` 统一完成（原子写回 level.dat）。

## 本目录模块

- **Pre261SaveEditor.cs**：数据版本 261 之前存档的编辑器实现（覆盖整个 1.x 体系）。
- **Version261PlusSaveEditor.cs**：数据版本 261 及之后（≥ `_261snapshot6`，即 2026 新版本号体系）存档的编辑器实现。

## 运行流程

1. 上层 `SaveManager.ApplyChangesAsync(folderPath, changes, ct)` 读取 level.dat、提取 `Data` 复合标签与 `DataVersion`。
2. 遍历注入的编辑器列表（默认 `[Pre261SaveEditor(), Version261PlusSaveEditor()]`），用 `CanHandle(dataVersion)` 选出唯一匹配者。
3. 调用 `ApplyChanges(data, changes)`，**原地修改**内存中的 `NbtCompound`（不写盘）。
4. 编辑器返回 `true` 表示至少一项被写入；`SaveManager` 随后执行原子写回（temp → 备份 `level.dat_old` → 重命名）。
5. 若 `changes.IsEmpty`（无待写字段）编辑器直接返回 `false`，`SaveManager` 也会提前短路避免文件 IO。

两个编辑器的分工：
- `Pre261SaveEditor` 负责「legacy 字段」：`Data.allowCommands`、`Data.Difficulty`（字节）、`Data.DifficultyLocked`（字节）。
- `Version261PlusSaveEditor` 在其基础上额外维护「新结构」：`difficulty_settings` 复合标签下的 `difficulty`（字符串）与 `locked`（字节），并复用前者的 `WriteAllowCommands` 保持旧字段同步。

## 输入输出规范

### ISaveEditor 契约（来自上层接口）
- `bool CanHandle(int? dataVersion)`：能否处理该 DataVersion（null 表示无 DataVersion 的老存档）。
- `bool ApplyChanges(NbtCompound data, SaveChanges changes)`：把 `changes` 写入 `data`，返回是否有写入。

### Pre261SaveEditor（`Pre261SaveEditor.cs`）
- `internal sealed class Pre261SaveEditor : ISaveEditor`。
- `CanHandle`：`dataVersion is null || dataVersion < DataVersionBoundaries._261snapshot6`（即 < 4774）。
- `ApplyChanges`：当 `changes.IsEmpty` 返回 `false`；否则依次尝试写入 `AllowCommands`、`Difficulty`、`DifficultyLocked`，用 `|=` 累计是否有改动。
- 三个 `internal static` 写入助手（均为"存在才写"策略）：
  - `WriteAllowCommands(data, changes)`：仅当 `changes.AllowCommands.HasValue` 且 `data` 已含 `allowCommands` 字段时，写入 `NbtByte(0/1)`。避免向 pre-1.3.1 存档凭空添加字段。
  - `WriteDifficulty(data, changes)`：仅当字段已存在时写入 `NbtByte`（0=和平/1=简单/2=普通/3=困难）。
  - `WriteDifficultyLocked(data, changes)`：仅当字段已存在时写入 `NbtByte(0/1)`。

### Version261PlusSaveEditor（`Version261PlusSaveEditor.cs`）
- `internal sealed class Version261PlusSaveEditor : ISaveEditor`。
- `CanHandle`：`dataVersion >= DataVersionBoundaries._261snapshot6`（≥ 4774）。
- `ApplyChanges`：
  - 确保 `data` 中存在 `difficulty_settings`（`NbtCompound`），不存在则新建并 `Add`；
  - 调用 `Pre261SaveEditor.WriteAllowCommands(data, changes)` 保持旧 `allowCommands` 同步；
  - 在 `difficulty_settings` 上写入 `difficulty`（字符串）与 `locked`（字节）。
- 两个 `internal static` 助手：
  - `WriteDifficulty(ds, changes)`：将 `Difficulty` 枚举映射为字符串 `peaceful/easy/normal/hard`（未知→`normal`），写入 `NbtString`。
  - `WriteLocked(ds, changes)`：写入 `NbtByte(0/1)`。

### 入参 SaveChanges（来自 `PCL.Core.Minecraft.Saves.Editing`）
`record struct SaveChanges`，字段均为 `Editable<T>`（`HasValue` 才写）：`AllowCommands`、`Difficulty`、`LockDifficulty`；`IsEmpty` 表示三项均无值。

## 使用约束与注意事项

- **仅内存操作**：编辑器不接触磁盘，也不负责压缩/备份；这些由 `SaveManager` 统一处理。不要在编辑器内自行读写 level.dat。
- **字段"存在才写"**：`Pre261SaveEditor` 故意只在目标字段已存在时才写入，以保护没有这些字段的旧版存档（如 pre-1.3.1 无 `allowCommands`、`Difficulty`）。若需为新存档初始化字段，当前实现不会主动创建。
- **新旧字段并行**：`Version261PlusSaveEditor` 同时维护 `Data.allowCommands`（旧）与 `difficulty_settings.*`（新），确保新旧游戏版本读取 level.dat 时都能识别。修改难度时务必两个入口都走，避免只改其一导致不一致。
- **难度表示差异**：旧版 `Difficulty` 是 `Data` 下的字节（0–3）；新版移到 `difficulty_settings.difficulty` 字符串。读取端（`Version261PlusSaveParser`）优先读字符串，回退到字节，两端约定需保持一致。
- **依赖项**：`fNbt`（NbtCompound/NbtByte/NbtString）、`DataVersionBoundaries`、`SaveChanges`、`ISaveEditor`、`Editable<T>` 均来自 `PCL.Core.Minecraft.Saves` 及 `Editing`；`NbtCompound.Contains`/`TryGet` 用于存在性判断。
- **线程安全**：编辑器为无状态 `sealed class`，`ApplyChanges` 只改传入的 `data` 对象，不共享可变字段，可安全并发于不同存档；但同一 `NbtCompound` 不应被多线程同时 `ApplyChanges`。
