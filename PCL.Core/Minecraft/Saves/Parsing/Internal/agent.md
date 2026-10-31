# Minecraft/Saves/Parsing/Internal 目录

存档解析器内部实现，位于命名空间 `PCL.Core.Minecraft.Saves.Parsing.Internal`。每个文件对应一个游戏数据版本区间的 NBT 解析逻辑，均实现 `ISaveParser` 接口，将 level.dat 的 `Data` 复合标签转换为不可变的 `SaveInfo`。解析器之间采用**装饰器链**：高版本解析器把低版本解析器作为 `_baseParser`，先调用 `base.Parse` 得到基础 `SaveInfo`，再用 `with` 表达式覆盖本版本特有字段。

## 本目录模块

- **NbtReadHelper.cs**：NBT 读取辅助（类型转换、字段取值）。
- **Pre113SaveParser.cs**：1.13 之前（含）存档解析器。
- **Version113To1152SaveParser.cs**：1.13–1.15.2 存档解析器。
- **Version19To1122SaveParser.cs**：1.9–1.12.2 存档解析器。
- **Version131To189SaveParser.cs**：1.3.1–1.8.9 存档解析器（版本区间特例）。
- **Version116To1211SaveParser.cs**：1.16–1.21.11 存档解析器。
- **Version261PlusSaveParser.cs**：数据版本 261 及之后存档解析器。

## 运行流程

1. 上层 `SaveManager.LoadFromPathAsync` 读取 level.dat（自动检测压缩），取 `Data` 复合标签，并从 `Data.DataVersion` 读出 `dataVersion`（无则为 `null`）。
2. `SaveParserFactory.Resolve(data, dataVersion)` 按**从高版本到低版本**的注册顺序遍历解析器，返回第一个 `CanHandle` 为 `true` 的解析器（默认顺序见 `SaveParserFactory`：261Plus → 116~1211 → 113~1152 → 19~1122 → 131~189 → Pre113）。
3. 调用该解析器的 `Parse(folderPath, data, createdAt, modifiedAt)`：
   - 装饰器链底端（如 `Version131To189SaveParser`/`Pre113SaveParser`）直接 `new SaveInfo { ... }`；
   - 上层解析器先 `var baseInfo = _baseParser.Parse(...)`，再用 `baseInfo with { ... }` 覆盖本版本变更字段后返回。
4. `Parse` 只做内存解析，不含文件 IO（除 `Version261PlusSaveParser` 会按需读取外部种子文件）；文件系统时间（`createdAt`/`modifiedAt`）由 `SaveManager` 传入。

各解析器匹配边界（`DataVersionBoundaries`）：
- `Pre113`：无 DataVersion 且无 `allowCommands`（Alpha~1.2.5）。
- `Version131To189`：无 DataVersion 但含 `allowCommands`（1.3.1~1.8.9）。
- `Version19To1122`：`[100 (_15w32a), 1443 (_17w47a))`（1.9~1.12.2）。
- `Version113To1152`：`[1443 (_17w47a), 2536 (_20w20a))`（1.13~1.15.2）。
- `Version116To1211`：`[2536 (_20w20a), 4774 (_261snapshot6))`（1.16~1.21.11）。
- `Version261Plus`：`>= 4774` 或存在 `difficulty_settings` 复合标签。

## 输入输出规范

### ISaveParser 契约（来自 `PCL.Core.Minecraft.Saves.Parsing`）
- `SaveFormatVersion FormatVersion { get; }`
- `bool CanHandle(NbtCompound data, int? dataVersion)`：能否处理。
- `SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)`：返回 `SaveInfo`（文件名时间来自调用方，NBT 内容来自 `data`）。

### NbtReadHelper（`NbtReadHelper.cs`，`internal static`）
- `long? TryGetLong(NbtCompound, key)`：读 `NbtLong`，缺失返回 null。
- `DateTime ReadLastPlayed(data)`：读 `LastPlayed` → `EpochMsToUtc`（Unix 毫秒转 UTC）。
- `DateTime EpochMsToUtc(long ms)`：`DateTime.UnixEpoch.AddMilliseconds(ms)`。
- `TimeSpan ReadPlayTime(data)`：读 `Time`（tick，20 tick=1 秒）→ `FromSeconds(ticks/20)`。
- `GameMode ReadGameMode(data, out bool isHardcore)`：hardcore 不是独立 GameType，而是 `Survival + hardcore=1`；`GameType` 1=创造/2=冒险/3=旁观/其它=生存。
- `Vector3? TryReadSpawnFromFields(data)`：旧格式 `SpawnX/Y/Z` 三个 `NbtInt`。
- `Vector3? TryReadSpawnFromPos(data)`：新格式 `spawn.pos` 的 `NbtIntArray`（长度须为 3）。
- `Difficulty? ReadDifficultyByte(data)`：读 `Difficulty` 字节（0–3），缺失返回 null。
- `(string? name, int? id) ReadVersion(data)`：读 `Version` 复合标签下的 `Name`(string) 与 `Id`(int)。

### 各解析器 Parse 关键差异
- **Pre113SaveParser**：`LevelName`、`RandomSeed`、`SpawnX/Y/Z` 形式出生点、`ReadGameMode`；`Difficulty=null`、`AllowCommands=false`（该版本无这些字段）。
- **Version131To189SaveParser**：在 Pre113 基础上补 `Difficulty`(字节)、`DifficultyLocked`、`allowCommands`（均为字节判断）。
- **Version19To1122SaveParser**：继承 131~189，额外 `ReadVersion` 填入 `VersionName`/`VersionId`。
- **Version113To1152SaveParser**：**纯透传**装饰器（仅标注格式，未额外提取 `DataPacks` 等字段），`Parse` 直接转发 `_baseParser`。
- **Version116To1211SaveParser**：覆盖 `Seed`（`ReadWorldGenSeed`：优先 `WorldGenSettings.seed`，回退 `RandomSeed`）与 `Spawn`（pos 数组优先，回退字段）。`ReadWorldGenSeed` 为 `internal static`。
- **Version261PlusSaveParser**：覆盖 `Seed`（WorldGenSettings 或外部文件）、`Spawn`、`Difficulty`（来自 `difficulty_settings.difficulty` 字符串，回退字节）、`IsHardcore`、`IsDifficultyLocked`、`GameMode`（hardcore 时置 `Hardcore`）。辅助方法均为 `internal static`：`ReadDifficultySettings`、`ReadHardcore`、`ReadLocked`、`ReadSeedFromExternalFile`。

### Version261PlusSaveParser 外部文件读取
`ReadSeedFromExternalFile(folderPath)`：尝试读 `<folderPath>/data/minecraft/world_gen_settings.dat`，用 `NbtFile` 读 `data.seed`；文件不存在或解析失败均返回 null（`catch { return null; }`，best-effort）。

## 使用约束与注意事项

- **装饰器链依赖**：每个"上层"解析器在默认构造函数里 `new` 其 `_baseParser`（如 `Version261PlusSaveParser()` → `new Version19To1122SaveParser()` → `…131To189`）。若通过 `SaveParserFactory(IEnumerable<ISaveParser>)` 注入自定义列表，请确保链底端解析器的 `CanHandle` 能兜底，否则 `Resolve` 可能返回 null（上层 `SaveManager` 会抛 `SaveCorruptedException`）。
- **匹配区间不可重叠**：`CanHandle` 用半开区间 + 无 DataVersion 特例判定；`Version113To1152` 虽是透传装饰器，仍必须能 `CanHandle` 对应区间，否则该版本段会落到更低版本解析器（丢失格式标记）。
- **NBT 版本兼容**：`TryGet`/`Contains` 用于安全取字段，缺失即返回 null/默认值；但 `LastPlayed`/`Time` 等核心字段若缺失会取 0（Epoch 起点或零时长），调用方需容忍。
- **外部文件风险**：`ReadSeedFromExternalFile` 对缺失/损坏文件静默返回 null，不会中断解析；但该路径会新开 `NbtFile` 读取，超大存档时注意一次性载入内存（见 `SaveManager` 已在 `Task.Run` 中异步读取以缓解 UI 阻塞）。
- **依赖项**：`fNbt`（NbtCompound/NbtLong/NbtInt/NbtIntArray/NbtString/NbtByte/NbtFile）、`DataVersionBoundaries`、`SaveInfo`、`SaveFormatVersion`、`GameMode`、`Difficulty`、`NbtReadHelper` 均来自 `PCL.Core.Minecraft.Saves`（及 `Parsing`）；`Vector3` 来自 `System.Numerics`。
- **线程安全**：解析器为无状态 `sealed class`，`Parse` 只读传入的 `data` 与（只读式）外部文件，可并发于不同存档；但要注意 `Version261PlusSaveParser.ReadSeedFromExternalFile` 会做文件 IO，多存档并行扫描时属正常并发读。
