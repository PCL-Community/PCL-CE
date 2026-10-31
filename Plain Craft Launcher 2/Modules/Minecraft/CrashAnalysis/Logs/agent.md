# Modules/Minecraft/CrashAnalysis/Logs

崩溃 **日志处理** 子模块：在分析前收集、导入并预处理游戏日志与崩溃报告。被 `CrashAnalyzer` 编排为"步骤 1（收集/导入）"与"步骤 2（预处理）"，为后续 `Detection` 与 `Export`/`Presentation` 提供数据。

## 本目录直接文件

- `CrashLogCollector.cs`：从版本目录中查找并收集可能的日志文件（步骤 1）。
- `CrashLogImporter.cs`：导入外部/用户提供的日志。
- `CrashLogPreparer.cs`：日志预处理（清洗、分段），生成 `CrashLogSet`。
- `CrashLogSet.cs`：日志集合（判断是否含可分析日志）。
- `CrashLogEntry.cs`：单条日志条目。
- `CrashLogKind.cs`：日志类型枚举（如 `latest.log`、崩溃报告等）。
- `CrashLogText.cs`：日志文本处理辅助。
- `CrashConfidence.cs`：分析置信度（用于标注结论可信程度）。

## 运行流程

### 步骤 1：收集（自动）`CrashLogCollector.Collect(versionPathIndie, IList<string>? latestLog)`

1. `_FindPossibleLogFiles` 列出候选路径：`<versionPathIndie>/crash-reports/*`、`versionPathIndie` 父目录的父目录下的 `*.log`、`versionPathIndie` 自身目录下的 `*.log`、`logs/latest.log`（始终加入）、若启动脚本 `LatestLaunch.bat` 含 `-Dlog4j2.formatMsgNoLookups=false` 则额外加入 `logs/debug.log`。
2. `IsRecentNonEmptyFile` 过滤：文件须存在、非空、且最后写入时间在 **3 分钟以内**，否则跳过（避免拿到陈旧日志）。
3. `TryAddLogFile` 将命中的文件读入 `context.RawFiles` 为 `CrashLogEntry`；读取失败仅 `LogWrapper.Warn`。
4. `AddCapturedOutput(latestLog)`：若存在 PCL 捕获的末尾输出（最后 200 行），写入 `TempFolder/RawOutput.log` 并加入 `RawFiles`；随后 `latestLog.Clear()`。

### 步骤 1：导入（手动/自主）`CrashLogImporter.Import(filePath)`

1. `_TryExtractArchive`：若文件是 zip/gz/bz2（见 `CrashFileIo.CanExtractArchive`）则解压到 `TempFolder/Temp`，否则直接复制原文件入 `Temp`。
2. 枚举 `Temp` 下所有文件：非空且扩展名为 `.log`/`.txt` 的加入 `context.RawFiles`；其余删除。单个文件失败仅 `Warn`。

### 步骤 2：预处理 `CrashLogPreparer.Prepare() -> bool`

1. 重置 `context.DirectOpenFile = null`、`PreparedLogs = null`、`OutputFiles.Clear()`。
2. `_ClassifyFiles`：按文件名与内容分类为 `CrashLogKind.Game/Debug/CrashReport/HsErr`，无法识别但形如 `.log`/`.txt` 的记为 `Extra`，其余 `Ignore` 跳过。
3. 选取 `DirectOpenFile`：优先非生成日志、再按最后写入时间倒序取首个可分析文件（供展示阶段"打开日志"使用）。
4. 若无任何可分析文件但有 Extra 文件，则把所有 Extra 当 `Game` 日志分析。
5. 选区构建 `CrashLogSet`：
   - `_SelectGameLog`：优先启动器日志（`rawoutput.log`、各 `启动器日志.txt` 等，仅取"以下为游戏输出的最后一段内容"之后的实时输出），再取 `latest.log`/`debug.log`（去头尾 `1500`/`500` 行）；兜底取最新游戏日志。
   - `_SelectDebugLog`：取最新 `Debug` 日志（头部 `1000` 行）。
   - `_SelectNewest(CrashReport, 300, 700)` 与 `_SelectNewest(HsErr, 200, 100)`：各自取最新一份，去头尾拼接文本。
   - `All` = `Game ?? Debug` + `HsErr` + `CrashReport` 文本拼接。
6. 写入 `context.PreparedLogs = logs`、`context.LogAll = logs.All`、把被选/额外文件全路径填入 `context.OutputFiles`。
7. 返回 `logs.HasAnalyzableLog`（即 `Game`/`CrashReport`/`HsErr` 任一非空）。

## 输入输出规范

### 数据模型

- `CrashLogEntry(string fullPath, IReadOnlyList<string> lines)`：`FullPath`、`Lines`、`FileName`（路径末段）。
- `CrashLogKind`：`Game` / `Debug` / `CrashReport` / `HsErr` / `All`。
- `CrashLogText`：`CrashLogKind Kind`、`string Text`、`string? FilePath`。
- `CrashLogSet`：`CrashLogText? Game/Debug/CrashReport/HsErr`、`string All`；`bool HasAnalyzableLog`；`string? GetText(CrashLogKind)` 按类型取文本（`All` 返回合并文本）。
- `CrashConfidence`：`High` / `Medium` / `Low`（本阶段仅定义枚举，实际取值在 `Detection` 赋值）。

### 关键方法签名

- `CrashLogCollector.Collect(string versionPathIndie, IList<string>? latestLog = null)`：写入 `context.RawFiles`。
- `CrashLogImporter.Import(string filePath)`：写入 `context.RawFiles`。
- `CrashLogPreparer.Prepare() : bool`：产出 `context.PreparedLogs`（`CrashLogSet`）、`context.OutputFiles`（`List<string>` 报告用文件全路径）、`context.DirectOpenFile`（`CrashLogEntry?`）、`context.LogAll`（`string`）；返回是否含可分析日志。

### 输入数据来源

- `versionPathIndie`：隔离/版本目录路径（由 `CrashAnalyzer.Collect` 透传）。
- `latestLog`：PCL 实时监控捕获的游戏标准输出末尾片段。
- 外部导入时由用户选择文件路径（可能含压缩包）。

### 输出产物

- `context.RawFiles`：原始日志条目集合（仅在 Collect/Import 阶段填充）。
- `context.PreparedLogs`：归一化、分类、裁剪后的日志集，供 `Detection` 消费。
- `context.OutputFiles`：将随报告导出（或仅存档）的文件全路径列表。
- `context.DirectOpenFile`：对话框"打开日志"按钮指向的文件。
- `Prepare()` 的 `bool` 返回值：告知上游是否有可分析日志。

### 异常

- 本阶段对 IO 失败（目录枚举、文件读取、时间获取）普遍使用 `try/catch + LogWrapper.Warn` 降级，不会因单个坏文件中断收集。
- 读取使用 `CrashFileIo`（底层 `FileShare.ReadWrite`，可读取正被 Logger 写入的文件；`ReadText` 经 `EncodingUtils.DecodeBytes` 自动探测编码）。不抛业务异常。

## 使用约束与注意事项

- **严格顺序**：必须先 `Collect`/`Import` 再 `Prepare`，且 `Prepare` 必须在 `CrashAnalyzer.Analyze` 之前（后者在 `PreparedLogs == null` 时抛 `InvalidOperationException`）。`Prepare` 会清空并重建 `PreparedLogs`/`OutputFiles`，重复调用安全但会覆盖。
- **时间窗过滤**：收集阶段只认 3 分钟内的日志文件；长时间未崩溃的实例可能漏收，需依赖"导入"路径。
- **编码与读取**：日志按原始字节读取并自动解码，行分割用 `"\r\n"`；若日志含异常行尾或被部分写入，`_GetHeadTailLines` 的去重（`seen` 集合）会剔除重复行，可能导致关键重复报错行被合并。
- **`debug.log` 触发条件**：仅当启动脚本含 `log4j2.formatMsgNoLookups=false` 才收集 `debug.log`，否则 Forge 调试日志相关的模组索引信息（`CrashModIndex._ReadForgeDebugLog`）将缺失。
- **文件名分类敏感性**：分类依赖固定的已知文件名白名单（`KnownGameLogNames`/`KnownDebugLogNames`/`KnownLauncherLogNames`）；新增日志命名（如非中文的启动器日志名）会被判为 `Extra` 或 `Ignore`，影响分析质量。
- **性能**：`Prepare` 对每条日志做去头尾行扫描（最多 `1500+700` 行级遍历）并 `Distinct`，文件多时有一定开销；属同步操作，由 `CrashAnalyzer` 单线程驱动。
- **`DirectOpenFile` 与生成日志**：优先选非生成（非 `TempFolder` 下）的日志，确保用户"打开"的是真实磁盘文件；若文件已不存在，`CrashDialogPresenter._OpenDirectFile` 会回退到临时 `Paths.Temp/Crash.txt`。
