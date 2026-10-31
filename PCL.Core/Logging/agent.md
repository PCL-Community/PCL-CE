# Logging 目录

日志子系统，位于命名空间 `PCL.Core.Logging`。提供统一的日志服务（写文件/转发的 `LogService`）与可扩展的 `Logger`/`LoggerAdapter` 体系，是整个核心库各服务的日志入口。

## 本目录模块

- **LogService.cs**：日志服务核心（`[LifecycleService]`，最高优先级），初始化 `Logger` 并接管全局日志包装。
- **LogWrapper.cs**：日志包装（全局静态日志入口，转发到 `LogService`）。
- **Logger.cs**：日志记录器（实际写日志、分级、格式化）。
- **LoggerAdapter.cs**：Logger 适配器（对接第三方日志框架）。
- **LoggerConfiguration.cs**：日志配置（路径、级别、滚动策略）。
- **LoggerFactoryAdapter.cs**：日志工厂适配器（创建 Logger）。
- **LoggerExtensions.cs**：Logger 扩展方法。
- **LogLevel.cs**：日志级别枚举。
- **ActionLevel.cs**：日志动作级别（用于决定日志触发的行为，如弹窗/上报）。
- **ExceptionDetails.cs**：异常详情模型（用于日志中结构化记录异常）。
