# Modules/Minecraft/CrashAnalysis/Export

崩溃 **报告导出** 子模块：把分析结果（`CrashAnalysisResult`）与上下文聚合为一份可读的 ZIP 报告，供用户在反馈或分享时使用。由 `CrashDialogPresenter._ExportReport` 调用（用户点击"导出报告"按钮时触发）。

## 本目录直接文件

- `CrashReportExporter.cs`：将分析结果（`CrashAnalysisResult`）与上下文导出为可读的报告文件，供用户在反馈或分享时使用。

## 运行流程

入口 `CrashReportExporter.Export(CrashAnalysisContext context, string targetZipPath, IEnumerable<string>? extraFiles)`，步骤：

1. 确保目标目录存在，若目标 ZIP 已存在则先删除。
2. 调用 `ModBase.FeedbackInfo()`（上报/反馈信息，具体行为依赖上游，待确认）。
3. 在 `context.TempFolder/Report` 下创建临时报告目录（已存在则 `CrashFileIo.DeleteDirectory` 清空）。
4. `_CollectOutputFiles`：把 `extraFiles`（来自展示层，如用户附加文件）并入 `context.OutputFiles`，逐个 `_CopyFileToReport` 复制到报告目录。
5. `_WriteEnvironmentInfo`：生成 `环境与启动信息.txt`（启动器版本/ID、玩家用户名、Java 信息、MC 文件夹、操作系统/CPU/GPU/内存等）。
6. `_WriteModInfo`：若 `context.Instance` 非空，扫描实例 `mods` 目录（含 Jar-in-Jar、依赖缺失/冲突、老 Forge 的版本子目录），生成 `模组列表.txt`。
7. `ZipFile.CreateFromDirectory(reportFolder, targetZipPath)` 打包；`finally` 中删除临时报告目录，确保即使失败也清理 `TempFolder`。

`_CopyFileToReport`：文件不存在则跳过；按原文件名映射导出名（`LatestLaunch.bat → 启动脚本.bat`、`RawOutput.log → 游戏崩溃前的输出.txt`（强制 UTF-8）、当前启动器日志 → `PCL CE 启动器日志.txt`（UTF-8），其余沿用原名）；编码经 `EncodingDetector.DetectEncoding` 探测；内容经 `_SanitizeFileContent` 脱敏（`McLogFilter.FilterAccessToken` 对启动脚本掩 `F`、其余掩 `*`；`McLogFilter.FilterUserName` 掩 `*`）。

报告中包含的固定文件名常量：`启动脚本.bat`、`游戏崩溃前的输出.txt`、`PCL CE 启动器日志.txt`、`环境与启动信息.txt`、`模组列表.txt`，外加 `context.OutputFiles` 中的原始日志。

## 输入输出规范

### 主要方法签名

- `CrashReportExporter.Export(CrashAnalysisContext context, string targetZipPath, IEnumerable<string>? extraFiles)`
  - 输入：`context`（需已填充 `OutputFiles`、`Instance`；`Result` 虽本阶段未直接读取，但报告面向整次分析）、`targetZipPath`（输出 ZIP 全路径）、`extraFiles`（附加文件路径，可为 `null`）。
  - 输出：在 `targetZipPath` 落地的 ZIP 文件；无返回值。方法本身未整体吞异常（见约束）。

### 依赖的上下文与全局对象

- `context.OutputFiles`：由 `CrashLogPreparer.Prepare` 填充的待导出文件列表。
- `context.Instance`（`McInstance?`）：用于模组扫描；为 `null` 时跳过 `模组列表.txt`。
- `ModJarInJarCache` / `ModLocalComp` / `ModJarInJarIndex`：实例模组与 Jar-in-Jar 扫描。
- `HardwareInfo` / `SystemInfo` / `Identify` / `Lang` / `Basics`：环境信息、本地化文本、版本号。
- `McLogFilter` / `EncodingDetector`：脱敏与编码探测。

### 异常

- 整体 `Export` 不被本类捕获；异常上抛至 `CrashDialogPresenter._ExportReport` 的 `try/catch`，在那里转为失败提示（`ExceptionDetails.Compose` + 可复制详情的 `MsgBox`）。
- `_WriteModInfo` 内部 `try/catch`：模组扫描失败仅 `LogWrapper.Warn`，不影响其余文件导出。
- `_CopyFileToReport` 对缺失文件直接跳过；`finally` 保证临时目录清理，避免 `TempFolder` 泄漏。

## 使用约束与注意事项

- **必须为最后阶段**：应在 `Logs.Prepare` 之后调用，以确保 `context.OutputFiles` 已填充；通常经由展示层按钮触发，此时 `Analyze` 也已完成。
- **UI 线程与阻塞**：保存路径选择 `_SelectReportSavePath` 通过 `ModBase.RunInUiWait` 在 UI 线程弹对话框；`Export` 本体（模组扫描、压缩）为同步 CPU/IO 密集操作，运行在调用线程上，可能短暂卡顿——不要在纯 UI 线程无提示地长时运行（展示层已在 `try/catch` 中处理失败，但未做异步进度提示，待确认）。
- **线程局部状态复位**：`_WriteModInfo` 用 `ModJarInJarCache.UseInstance(instance)` 设置线程级"当前实例"，并在 `finally` 中 `UseInstance(null)` 复位、清空 `ModLocalComp.JijScanContext`；不要在并发线程复用同一实例缓存，否则会路由错误。
- **脱敏要求**：所有导出内容均经 token/用户名脱敏，新增写入报告的文件务必也走 `_SanitizeFileContent` 或等价过滤，防止泄露 `accessToken` 或用户名。
- **编码一致性**：`RawOutput.log` 与启动器日志强制 UTF-8 写入；其余文件保留探测到的原始编码，避免中文日志二次乱码。
- **`extraFiles` 合并时机**：`_CollectOutputFiles` 直接把 `extraFiles` 追加进 `context.OutputFiles`（会修改上下文），多次导出会累积——调用方需注意幂等性。
