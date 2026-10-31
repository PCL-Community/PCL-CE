# App 测试分组

本目录覆盖 PCL.Core 中 `PCL.Core.App` 命名空间下的应用层组件测试，包含命令行解析、配置存储与事件总线。

## 本目录测试模块

- `CommandLineTest.cs` — `PCL.Core.App.Cli.CommandLine` 子命令解析与序列化（`PCL.Core.Test.App` 命名空间，`sealed class`）。
  - 覆盖模块：`PCL.Core.App.Cli`（命令树解析 `CommandLine.Parse`、嵌套子命令定义 `SubcommandDefinition`）、`PCL.Core.Utils`（`JsonCompat.SerializerOptions`）。
  - 测试编排（构造函数 `CommandLineTest`）：以 `SubcommandDefinition` 声明 `foo→bar→foo` 与 `bar→foo` 两层子命令树，传入参数数组 `["bar","foo","--f","--bar","foo","bar","--foo","foo","1234","5678"]` 在构造时即调用 `CommandLine.Parse` 得到 `_model`，供各用例复用。
  - 关键用例：
    - `Parse`：断言 `_model.ToString()` 与预期缩进文本 `_correct`（多层 `-> 名称 [--选项: 值]` 树形结构）完全一致，验证嵌套子命令与选项的解析/输出。
    - `JsonSerialization`：用 `JsonSerializer`（`JsonCompat` 选项，缩进）将 `_model` 序列化后反序列化为 `CommandLine`，再次 `ToString()` 仍等于 `_correct`，验证 JSON 往返不丢失命令树结构。
  - 注意：当前仅覆盖成功路径与往返一致性；非法参数、未知子命令、选项类型错误等负向用例未在本文件体现（待确认是否由其他用例或后续补充覆盖）。

## 运行流程（测试编排）

- 唯一测试类 `CommandLineTest` 标注 `[TestClass]`，构造函数完成命令树定义与 `Parse`（类级一次性初始化，非 `[TestInitialize]`）。
- 两个 `[TestMethod]`（`Parse`、`JsonSerialization`）共享构造函数产出的 `_model`，无文件/网络夹具，纯内存断言。
- 序列化用例使用 `JsonSerializerOptions(JsonCompat.SerializerOptions) { WriteIndented = true }`，与 PCL.Core 统一 JSON 约定保持一致。

## 输入输出规范（被测 API 与夹具）

- 被测 API：`CommandLine.Parse(string[] args, IEnumerable<SubcommandDefinition> subcommands)`、`CommandLine.ToString()`、`JsonSerializer.Serialize/Deserialize<CommandLine>`。
- 输入：`testArgs` 模拟真实启动参数、`subcommands` 描述两层命令树；选项含布尔 `--f`、字符串 `--bar`、键名数字 `--1234` 等形态。
- 输出：预期 `_correct` 为标准化的树形文本；反序列化后模型 `ToString()` 须与之一致。
- 断言风格：`Assert.AreEqual(_correct, modelStr)` 文本全量比对，外加 `Console.WriteLine` 便于人工核对。

## 使用约束与注意事项

- 纯离线、确定性用例，无需联网或运行时资源，可在 CI 稳定执行。
- 依赖 `JsonCompat.SerializerOptions`（PCL.Core 全局 JSON 选项）；若 PCL.Core 调整该选项（如命名策略），预期文本可能变化，需同步更新 `_correct` 常量。
- 本目录未覆盖的负向/边界场景（如参数缺失、重复选项、未知子命令的异常行为）建议后续补充或确认已有覆盖位置。

## 子目录导航

- [Configuration](./Configuration/agent.md) — 配置存储相关测试。
- [EventBus](./EventBus/agent.md) — 事件总线服务测试。
