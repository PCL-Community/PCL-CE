# Utils 目录

通用工具库，位于命名空间 `PCL.Core.Utils`。聚合启动器各处复用的底层工具：参数构建、原子/并发类型、编解码、差异、加密、扩展方法、哈希、OS 互操作、密钥、线程、校验与 WinRT 互操作。多为静态/无状态辅助类。

## 本目录模块

- **`ArgumentsBuilder.cs`** — 命令行参数构建 `ArgumentsBuilder`：`Add`/`AddWithSpace`/`AddFlag`/`AddIf`/`AddWithSpaceIf`/`AddFlagIf`/`GetResult`/`Clear`，配合 `ArgumentStyle`/`Argument`/`PrefixStyle`。
- **`AtomicVariable.cs`** — 原子变量 `AtomicVariable`：`SetValue` 等线程安全读写。
- **`ConcurrentSet.cs`** — 并发集合 `ConcurrentSet`：`TryAdd`/`TryTake`/`Add`/`Remove`/`Contains`/`Clear`/`ToArray` 等线程安全 Set 操作。
- **`EaseUtils.cs`** — 缓动工具 `EaseUtils`：`Bounce` 等缓动计算。
- **`ExpandoObjectConverter.cs`** — 动态对象转换 `ExpandoObjectConverter`：`Read`/`Write` 在 `ExpandoObject` 与字典/JSON 间转换。
- **`FrameUtils.cs`** — 帧/时间工具 `FrameUtils`：`NowStamp`、`StampToFrameIndex`、`TimeSpanToFrameIndex`。
- **`IconHelper.cs`** — 图标辅助 `IconHelper`：`GetIconPath` 解析图标路径。
- **`InvalidJsonException.cs` / `NotFoundException.cs`** — 专用异常类型（JSON 解析失败 / 资源未找到）。
- **`JsonCompat.cs`** — JSON 兼容层 `JsonCompat`：`ParseNode`/`ToObject`/`FromObject`、`TryGetDateTime`/`TryParseDateTime`/`NormalizeDateTime`、`Merge`，以及 `FlexibleDateTimeConverter`/`FlexibleBoolConverter`/`FlexibleStringConverter`。
- **`PEHeaderReader.cs`** — PE 头读取 `PEHeaderReader`：`ReadPEHeader`/`IsMachine64Bit`，配合 `MachineType`/`PEStruct` 判定可执行文件位数/平台。
- **`ParameterizedProperty.cs`** — 参数化属性 `ParameterizedProperty`：带元数据的依赖属性封装。
- **`RandomUtils.cs`** — 随机工具 `RandomUtils`：`PickRandom`/`NextInt`/`Shuffle`/`ShuffleInPlace`。
- **`RegexPatterns.cs`** — 常用正则常量 `RegexPatterns`。
- **`Result.cs`** — 通用结果类型 `Result`：`Match`/`IfSuccess`/`IfFailure`/`OrElse`/`Expect`/`UnwrapOrThrow`（类 Rust `Result<TOk,TErr>`）。
- **`SemVer.cs`** — 语义化版本 `SemVer`：`Parse`/`TryParse`/`CompareTo`/`ToString`。
- **`SimilaritySearch.cs`** — 相似度搜索 `SimilaritySearch`：`Search`，配合 `SearchEntry`。
- **`StringStream.cs`** — 字符串流 `StringStream`：`Read`/`Write`/`Seek`/`SetLength`/`Flush`，流式读写文本。
- **`TimeUtils.cs`** — 时间工具 `TimeUtils`：`GetTimeNow`/`GetTimeTick`/`GetUnixTimestamp`/`FromUnixTimestamp`/`ToLocalTime`。
- **`VarIntHelper.cs`** — VarInt 编解码 `VarIntHelper`：`Encode`/`Decode`/`DecodeUInt`/`DecodeZigZag`/`EncodeZigZag`、`ReadFromStreamAsync`/`ReadUIntFromStreamAsync`。
- **`VersionRange.cs`** — 版本区间 `VersionRange`：`IsInRange`/`SetMin`/`SetMax`。
- **`WpfUtils.cs`** — WPF 辅助 `WpfUtils`：`IsDependencyPropertySet` 等视觉树/属性工具。

## 直接代码要点

- **运行流程**：这些工具多为无状态静态类，由上层按需调用；`ArgumentsBuilder` 链式拼接命令行，`VarIntHelper`/`StringStream` 处理 Minecraft 协议与文本流，`JsonCompat` 统一 JSON 读写与日期容错。
- **输入输出规范**：输入/输出以字符串、字节、流、`JsonNode` 为主；`Result` 以成功/失败分支表达错误；`SemVer`/`VersionRange` 提供比较与区间判定。
- **使用约束与注意事项**：`ConcurrentSet`/`AtomicVariable` 保证线程安全，但复合操作仍需外层同步；`JsonCompat` 的日期解析有多种边界（`-00:00`、`24:00:00`）；VarInt 解码需处理截断/超长；WPF 相关工具须在 UI 线程。

## 子目录导航

- [Codecs](./Codecs/agent.md)：文本编码检测与编解码。
- [Diagnostics](./Diagnostics/agent.md)：诊断辅助。
- [Diff](./Diff/agent.md)：差异计算。
- [Encryption](./Encryption/agent.md)：对称加密提供器。
- [Exts](./Exts/agent.md)：扩展方法。
- [Hash](./Hash/agent.md)：哈希算法。
- [OS](./OS/agent.md)：操作系统互操作。
- [Secret](./Secret/agent.md)：密钥与标识。
- [Threading](./Threading/agent.md)：线程基元与线程池。
- [Validate](./Validate/agent.md)：输入校验器。
- [WinRT](./WinRT/agent.md)：WinRT 互操作。
