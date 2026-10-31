# Modules/Minecraft/CrashAnalysis/Detection

崩溃 **原因检测** 子模块：在日志预处理完成后，依据规则与异常栈定位崩溃根因。被 `CrashAnalyzer.Analyze` 调用（`CrashAnalyzer.cs:64` 把 `logs` 与 `version` 交给 `_detector.Analyze`）。

## 本目录直接文件

- `CrashDetector.cs`：检测编排器，组合证据提取与栈分析，产出 `CrashAnalysisResult`；无可用日志时直接给出高置信度结论。
- `CrashEvidenceExtractor.cs`：从日志中抽取关键证据（错误片段、版本、模组等）。
- `CrashStackAnalyzer.cs`：分析异常栈，定位抛出点。
- `CrashRule.cs` / `CrashRuleCatalog.cs`：单条检测规则与规则目录（如常见 Mod 冲突、内存不足、缺失依赖等）。
- `CrashModIndex.cs`：模组索引，用于将证据关联到具体模组。
- `DetectionPhase.cs`：检测阶段枚举。

## 运行流程

入口为 `CrashDetector.Analyze(CrashLogSet logs, McInstance? instance) -> CrashAnalysisResult`，固定执行下列步骤（参考 `CrashAnalyzer.cs` 标注的"步骤 3"）：

1. **无可分析日志短路**：若 `logs.HasAnalyzableLog` 为 `false`，直接 `Add(new CrashFinding(CrashCause.NoAnalyzableFile, CrashConfidence.High){ ShouldStop = true })` 并立即返回，不再做任何规则匹配。
2. **日志归一化 `_Normalize`**：仅当全量文本同时包含 `quilt`（忽略大小写）与 `Mod Table Version` 时，对 Quilt Mod Table 做裁切（以 `| Index` 为前界、`Mod Table Version:` 为后界重拼），避免表格噪声干扰后续匹配；其余情况原样保留。
3. **构建模组索引 `CrashModIndex.Create(normalizedLogs, instance)`**：从崩溃报告、Forge Debug 日志、加载器消息、实例 `mods` 目录聚合候选模组（详见 `CrashModIndex`）。
4. **阶段化运行 `_RunPhase`**，按严格优先级依次执行，每阶段结束后检查 `_ShouldStop`（存在任意 `ShouldStop == true` 的 finding 即提前返回）：
   - `DetectionPhase.Fatal`：致命规则（Java 版本、OptiFine 不兼容、显卡驱动、内存等）。
   - `DetectionPhase.Primary`：主因规则（Mixin 失败、Forge/Fabric 错误、确认/疑似模组崩溃等）。
   - **栈分析 `CrashStackAnalyzer.Analyze`**：在 Primary 之后、Secondary 之前独立运行，返回 `StackModNameFound` 或 `StackKeywordFound`（均 `ShouldStop = true`）。
   - `DetectionPhase.Secondary`：次要规则（输出过短、加载器错误、特定方块/实体等）。
5. 每个 `_RunPhase(phase, …)` 内部先遍历 `CrashRuleCatalog.Rules` 中 `Phase == phase` 的规则并 `Evaluate`，再调用 `CrashEvidenceExtractor.Extract(phase, logs, modIndex)` 补充证据，命中的 `CrashFinding` 经 `_AddAndLog` 加入结果并写日志。
6. 结束前调用 `_LogSummary` 汇总找到的原因数量（或"未找到"）。

`_RunPhase` 阶段顺序与 `_ShouldStop` 短路共同保证：高置信致命原因命中后不再产出次要噪声结论；但同一 `CrashCause` 的多个 finding 会在 `CrashAnalysisResult.Add` 中合并证据（`ShouldStop` 取或）。

## 输入输出规范

### 主要类与方法签名

- `CrashDetector.Analyze(CrashLogSet logs, McInstance? instance) : CrashAnalysisResult`
  - 输入：`logs` 来自 `CrashAnalysisContext.PreparedLogs`（必须非空，否则上游 `CrashAnalyzer.Analyze` 抛 `InvalidOperationException`）；`instance` 可为 `null`，仅用于增强模组索引。
  - 输出：`CrashAnalysisResult`（内含 `Findings` 列表，详见父目录模型 `CrashFinding`/`CrashAnalysisResult`）。
- `CrashEvidenceExtractor.Extract(DetectionPhase phase, CrashLogSet logs, CrashModIndex modIndex) : IEnumerable<CrashFinding>`
  - 按 phase 分派到 `_ExtractFatal` / `_ExtractPrimary` / `_ExtractSecondary`（`_` 私有方法名见 `agent.md` 下"证据提取器"小节），各方法返回 `CrashFinding?` 或 `IEnumerable<CrashFinding>`。
- `CrashStackAnalyzer.Analyze(CrashLogSet logs, CrashModIndex modIndex) : CrashFinding?`
  - 仅当全量文本包含 `forge`/`fabric`/`quilt`/`liteloader`（忽略大小写）才分析，否则返回 `null`（"可能并未安装 Mod，不进行堆栈分析"）。
- `CrashRule` 字段：`CrashCause Cause`、`DetectionPhase Phase`、`CrashConfidence Confidence`（默认 `High`）、`bool StopOnMatch`、`Func<CrashRuleInput, CrashFinding?> Evaluate`。
- `CrashRuleInput`：`CrashLogSet Logs`、`CrashModIndex ModIndex`（传递给每条规则的 `Evaluate`）。
- `CrashRuleCatalog.Rules : IReadOnlyList<CrashRule>`：静态只读规则表，由 `_Contains` / `_ContainsAny` / `_ContainsAll` 工厂构建（分别匹配单串、任一串、全部串），命中即产出 `CrashFinding(Cause, High, [evidence{Pattern=pattern, DisplayKind="pattern"}]){ ShouldStop = stopOnMatch }`。

### 关键产物与数据对象

- `CrashFinding`：构造 `CrashFinding(CrashCause cause, CrashConfidence confidence, IEnumerable<CrashEvidence>? evidence = null)`；可变属性 `ShouldStop`；只读 `Details`（`Evidence` 中非空 `Value` 去重列表）。`CrashEvidence` 字段：`Value`、`CrashLogKind? Source`、`Pattern`、`DisplayKind`。
- `CrashConfidence`：`High` / `Medium` / `Low`（栈分析命中有具体模组名为 `Medium`，仅有关键词为 `Low`）。
- `CrashCause`：稳定枚举（约 47 个值，如 `OutOfMemory`、`ConfirmedModCrash`、`ModMixinFailed`、`StackModNameFound` 等，见 `CrashCause.cs`）。

### 异常

- 本阶段不直接抛业务异常。所有正则走 `CrashRegex`（内部 `try/catch` 返回 `null`/空列表）；栈分析的 `GeneratedRegex` 为编译期生成、无动态失败面。异常仅在 `CrashModIndex._ReadInstanceModFiles` 等 IO 处被 `LogWrapper.Warn` 吞掉。
- 上游保证：`CrashAnalyzer.Analyze` 在 `PreparedLogs == null` 时抛 `InvalidOperationException`（"Prepare must be called before Analyze"）。

### 证据提取器（`CrashEvidenceExtractor`）分类速查

- `_ExtractFatal`：内存不足（`OutOfMemory`/`X86JavaMemoryLimit`）、访问越界（`Intel/Amd/NvidiaDriverAccessViolation`）、签名校验失败、OptiFine 缺失模组、确认模组崩溃、重复模组、缺失依赖。
- `_ExtractPrimary`：Mixin 失败（`ModMixinFailed`）、Forge 错误、Fabric 提供的解决方案、Fabric 提供模组崩溃、疑似模组（`SuspectedModCrash`）。
- `_ExtractSecondary`：输出过短（`VeryShortOutput`）、加载器错误、模组初始化失败、特定方块/实体崩溃。
- 多数为 `ShouldStop = true` 的 `High` 置信结论；部分（如 `VeryShortOutput`、`ModInitializationFailed`）为 `false` 以示"可继续累积"。

## 使用约束与注意事项

- **前置依赖**：必须先完成 `Logs` 阶段的 `Collect`/`Import` 与 `Prepare`，否则 `PreparedLogs` 为 `null` 会触发 `InvalidOperationException`。`CrashDetector` 本身不读取上下文，仅消费传入的 `logs`。
- **阶段顺序与短路**：`Fatal → Primary → 栈分析 → Secondary` 不可调换；一旦 `_ShouldStop` 命中，后续阶段立即中止。新增规则时务必正确设置 `Phase` 与 `StopOnMatch`，否则会污染结论或漏报。
- **模组索引依赖 `McInstance`**：`CrashModIndex.Create` 会枚举实例 `mods` 目录（含隔离路径）。`instance` 为 `null` 时只能靠日志内嵌的模组信息解析（`ResolveToDisplayNames` 回退为原始 hint）；涉及模组名的结论（如 `ConfirmedModCrash`）会因此精度下降。
- **Quilt 归一化易错点**：`_Normalize` 依赖 `quilt` + `Mod Table Version` 同时出现，且用固定锚点 `| Index` / `Mod Table Version:` 裁切；若锚点随版本变化，归一化将失效并影响后续匹配。
- **栈分析阈值**：`_ExtractKeywords` 关键词数若为 `0` 或 `>10` 则整体返回 `null`（>10 视为"匹配出错不纳入"）。`CrashStackAnalyzer` 的 `IgnoredPrefixes`/`IgnoredWords` 决定哪些包被忽略，新增主流模组/加载器命名空间时需同步维护，否则会被误判为"可疑 Mod"。
- **Mixin 与 Fabric 冲突**：`_ExtractFabricProvidedModCrash` 在 `_LooksLikeMixinFailure` 为真时主动跳过（交给 Mixin 分支），改写相关逻辑需注意两分支的优先级。
- **线程与性能**：单线程由 `CrashAnalyzer` 驱动；`CrashRuleCatalog.Rules` 在类型初始化时构建一次（`static` 只读），规则匹配与大量正则（含 `RegexOptions.IgnoreCase`、部分 `Compiled`）作用于可能很大的日志文本，属同步 CPU 密集操作。
