# App/IoC 目录

依赖注入与生命周期（IoC）子系统，位于命名空间 `PCL.Core.App.IoC`。是整个 PCL.Core 的服务编排核心：通过 `[LifecycleService]` / `[LifecycleScope]` 等特性标记服务，由源生成器驱动在应用启动/停止阶段按状态与优先级统一启停；并提供依赖解析与属性访问能力。

## 本目录模块

- **Attributes.cs**：生命周期与依赖注入特性（如 `[LifecycleService]`、`[LifecycleScope]`、`[LifecycleStop]`）。
- **Lifecycle.cs**：生命周期总控（运行服务表、启停事件、服务栈）。
- **LifecycleState.cs**：生命周期状态枚举（源生成器 `AdditionalFiles` 引用）。
- **LifecycleContext.cs**：生命周期上下文（提供给各服务的日志与状态访问）。
- **LifecycleFlow.cs / LifecycleProcess.cs / LifecycleLogs.cs**：生命周期流程编排、过程执行与日志。
- **LifecycleService.cs**：生命周期服务聚合入口。
- **DependencyGroup.cs / DependencyGroups.cs**：依赖分组（单例/瞬态等）。
- **DependencyArguments.cs**：依赖注入构造参数。
- **GeneralService.cs**：通用服务基类/辅助。
- **PropertyAccessor.cs**：属性访问器（反射读写服务属性）。
