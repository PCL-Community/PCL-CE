# Modules/Minecraft/CrashAnalysis/Presentation

分析结果 **展示** 子模块：将崩溃分析结论格式化并以对话框呈现给用户。由 `CrashAnalyzer.Output` 调用，是崩溃分析流水线（`收集→导入→预处理→检测→导出/展示`）的最后一环。

## 本目录直接文件

- `CrashDialogPresenter.cs`：弹出崩溃分析对话框，组合 `CrashReportExporter` 与 `CrashResultFormatter` 输出结果（支持手动分析模式）。
- `CrashResultFormatter.cs`：将 `CrashAnalysisResult` 格式化为可读文本/结构化内容。
- `CrashMessageSpec.cs`：消息规格，描述对话框标题、正文、按钮等展示参数。

## 运行流程

### 入口 `CrashDialogPresenter.Output(bool isHandAnalyze, List<string>? extraFiles)`

1. `ModMain.frmMain.ShowWindowToTop()` 把主窗口提到最前。
2. `_formatter.Format(context, isHandAnalyze)` 得到 `CrashDialogContent`（含 `Text` 与 `SuggestedAction`）。
3. 取 `context.DirectOpenFile`；计算 `openInstanceSettings = context.Instance != null && suggestedAction == OpenInstanceSettings`。
4. 组装标题（手动 `Crash.Dialog.Title.Manual` / 自动 `Crash.Dialog.Title.Auto`）与按钮：
   - 第一按钮：确认（`Common.Action.Confirm`，编号 1）。
   - 第二按钮：手动模式或无 directFile 时为空；否则 `前往修改`（若 `openInstanceSettings`）或 `打开日志`。
   - 第三按钮：仅自动模式显示 `导出报告`（`Crash.Dialog.Button.ExportReport`）。
5. `MsgBoxWrapper.ShowWithCustomButtons(...)` 弹出；按返回值：编号 2 → 打开实例设置页或打开 directFile；编号 3 → `_ExportReport(extraFiles)`。
6. `_ExportReport`：`_SelectReportSavePath`（`ModBase.RunInUiWait` 弹保存对话框）拿到路径后调用 `_exporter.Export(context, fileAddress, extraFiles)`，成功弹 `HintWrapper` 并打开所在目录；失败则 `LogWrapper.Error` 并用 `ExceptionDetails.Compose` 展示可复制详情的对话框。

### 格式化 `CrashResultFormatter.Format(CrashAnalysisContext context, bool isHandAnalyze) -> CrashDialogContent`

1. `result = context.Result ?? new CrashAnalysisResult()`。
2. 若 `!result.Any`：返回"未知原因"文案（手动/自动两套 `Crash.Reason.Unknown.*`），`SuggestedAction.None`。
3. 否则对每个 `finding` 经 `_CreateMessageSpec(finding, context.LogAll)` 生成 `CrashMessageSpec`，再 `_Render` 成文本段落。
4. 非手动模式时 `_AppendFollowUps`：依据各 spec 的 `AppendReportHint`/`AppendHelpHint` 追加"查看报告"/"导出报告"提示，并附加启动器过旧建议（经 `UpdateManager.GetVersionStatus`）。
5. 返回 `CrashDialogContent(归一化换行后的 text, specs[0].SuggestedAction)`——**首条 spec 的 `SuggestedAction` 决定全局按钮行为**（如 `OpenInstanceSettings` 会让第二按钮变为"前往修改"）。

`_CreateMessageSpec` 是大型 `switch(finding.Cause)`，把每个 `CrashCause` 映射到本地化键（`Crash.Reason.*`/`Crash.Suggestion.*`），并按证据条数选择 Single/Multiple 文案，设置 `report`（追加报告提示）、`help`（追加帮助提示）、`action` 等标志。

## 输入输出规范

### 主要类与方法签名

- `CrashDialogPresenter(CrashAnalysisContext context)`：构造时持有 `CrashReportExporter` 与 `CrashResultFormatter`。`Output(bool isHandAnalyze, List<string>? extraFiles)` 为公开入口。
- `CrashResultFormatter.Format(CrashAnalysisContext context, bool isHandAnalyze) : CrashDialogContent`。
- `CrashMessageSpec(string reasonKey, object?[] reasonArgs, string? suggestionKey, object?[] suggestionArgs, bool appendReportHint, bool appendHelpHint, CrashSuggestedAction suggestedAction = None)`：只读属性 `ReasonKey`/`ReasonArgs`/`SuggestionKey`/`SuggestionArgs`/`AppendReportHint`/`AppendHelpHint`/`SuggestedAction`。
- `CrashDialogContent(string text, CrashSuggestedAction suggestedAction)`：`Text`、`SuggestedAction`。
- `CrashSuggestedAction` 枚举：`None` / `OpenInstanceSettings`。

### 输入数据来源

- `context.Result`：由 `Detection` 产出的 `CrashAnalysisResult`（为 `null` 时按"未知"处理）。
- `context.LogAll`：合并日志文本，供 `_CreateMessageSpec` 判断显卡类型（如是否含 `hd graphics `）等上下文。
- `context.DirectOpenFile` / `context.Instance`：决定第二按钮文本与动作。
- `isHandAnalyze`：区分"自动崩溃分析"与"用户手动分析已有日志"两种模式（影响标题、未知文案、是否显示导出按钮与后续提示）。

### 输出产物

- 弹出的 `MsgBox`（含至多三个自定义按钮）作为直接用户交互。
- 可选落地的崩溃报告 ZIP（经 `CrashReportExporter`）。
- `CrashDialogContent.Text` 为最终呈现给用户的纯文本结论（已做换行归一化 `\r\n`）。

### 异常

- `_ExportReport` 整体 `try/catch`：失败仅提示，不中断对话框。
- `_GetLauncherOutdatedSuggestion` 在 `UpdateManager.GetVersionStatus` 抛错时 `LogWrapper.Error` 并返回 `null`（降级为不追加提示）。
- `_OpenDirectFile` 对不存在的 directFile 回退写临时 `Paths.Temp/Crash.txt` 再打开。

## 使用约束与注意事项

- **必须在分析与预处理之后**：依赖 `context.Result` 与 `context.DirectOpenFile`，应作为流水线的收尾调用。
- **UI 线程约束**：`Output` 内含 `ShowWindowToTop`、`MsgBoxWrapper.ShowWithCustomButtons`、`ModBase.RunInUiWait`（保存对话框）与 `ModBase.RunInUi`（打开设置页），必须在 UI 线程执行；`CrashResultFormatter.Format` 本身为纯文本计算，可离线调用，但常规路径也在 UI 线程。
- **`SuggestedAction` 取首条**：全局"前往修改"行为由 `specs[0]` 决定，因此结论排序（由 `Detection` 的添加顺序与去重合并决定）直接影响按钮文案与跳转——新增原因时不要在 `None` 之外随意设置 `OpenInstanceSettings`，以免错误引导。
- **本地化键耦合**：`_CreateMessageSpec` 的 `switch` 与大量 `Lang.Text("Crash.Reason.*"/"Crash.Suggestion.*")` 键强耦合资源文件；新增 `CrashCause` 必须同步补充对应键，否则显示缺翻译（落入 `default` 分支显示 `UnknownFinding` 并附带枚举名）。
- **手动分析模式差异**：手动模式下第二按钮恒为空、不显示导出按钮、未知文案不同，且 `_AppendFollowUps` 被跳过（只展示原因本身）。
- **换行归一化**：`_NormalizeLineEndings` 统一为 `\r\n`，编写/拼接多语言模板时注意避免重复空行。
- **性能**：`Format` 为同步字符串拼接，开销极小；真正的重活在 `Export`（见 `Export/agent.md`），展示层不做阻塞式模组扫描。
