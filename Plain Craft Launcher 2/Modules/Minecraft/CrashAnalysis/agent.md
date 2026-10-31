# Modules/Minecraft/CrashAnalysis

启动 **崩溃分析子系统**，在游戏启动失败/崩溃时自动收集日志、定位原因并向用户展示可读结果。流程由 `CrashAnalyzer` 编排：收集 → 导入 → 预处理 → 检测 → 导出/展示。

## 本目录直接文件

- **`CrashAnalyzer.cs`** — 分析流程编排器 `CrashAnalyzer`：`Collect`（收集日志）→ `Import`（导入压缩包）→ `Prepare`（预处理）→ `Analyze`（检测）→ `Output`（展示/导出），组合各阶段实现。
- **`CrashAnalysisContext.cs`** — 分析上下文 `CrashAnalysisContext`：进程 ID、临时目录、原始/输出文件列表、关联 `McInstance`。
- **`CrashAnalysisResult.cs`** — 分析结果集合 `CrashAnalysisResult`：`Add` 归集发现项。
- **`CrashCause.cs` / `CrashEvidence.cs` / `CrashFinding.cs`** — 崩溃原因 `CrashCause`、证据 `CrashEvidence`（`EqualsTo`）、发现项 `CrashFinding`（`AddEvidence`）。
- **`CrashFileIo.cs`** — 文件 IO 工具 `CrashFileIo`：`ReadBytes`/`ReadText`/`WriteText`/`CopyFile`/`DeleteDirectory`/`CanExtractArchive`/`ExtractFile`。
- **`CrashText.cs`** — 文本处理 `CrashText`：`BeforeFirst`/`AfterFirst`/`AfterLast`/`Between` 等截取工具。
- **`CrashRegex.cs`** — 正则工具 `CrashRegex`：`First`/`All`/`IsMatch`。

## 直接代码要点

- **运行流程**：`CrashAnalyzer.Collect` 收集候选日志 → `Import` 解压导入 → `Prepare` 归一化/筛选出可分析日志 → `Analyze` 调用 `Detection` 产出 `CrashAnalysisResult` → `Output` 经 `Presentation` 展示并可选 `Export` 报告。
- **输入输出规范**：输入为崩溃进程/实例与日志文件；输出为 `CrashAnalysisResult`（原因/证据/发现项）与可读报告；上下文贯穿各阶段。
- **使用约束与注意事项**：各阶段有严格顺序，前一步产物是后一步输入；日志编码需自动探测；规则误判需靠证据链降低；导出失败不应阻断展示；UI 展示须在 UI 线程。

## 子目录导航

- [Detection](./Detection/agent.md)：崩溃原因检测（规则与栈分析）。
- [Export](./Export/agent.md)：崩溃报告导出。
- [Logs](./Logs/agent.md)：崩溃日志收集与预处理。
- [Presentation](./Presentation/agent.md)：分析结果格式化与对话框展示。
