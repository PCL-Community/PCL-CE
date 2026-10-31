# ViewModel 目录

MVVM 视图模型容器，位于命名空间 `PCL.Core.ViewModel`。聚合启动器各界面所需的视图模型（目前为首页相关），并提供视图模型基类与异步状态模型供子目录复用。

## 本目录直接代码

- **`AsyncState.cs`** — 异步状态模型 `AsyncState`：用于在视图模型中表达异步操作的状态（如空闲/进行中/完成/错误），作为 VM 与界面绑定的状态载体。
- **`ViewModelBase.cs`** — MVVM 基类 `ViewModelBase`：为派生视图模型提供受保护的 `RunAsync` 异步执行辅助，统一封装异步任务的启动与状态流转，避免各 VM 重复编写样板代码。

## 直接代码要点

- **运行流程**：派生 VM 继承 `ViewModelBase` → 在命令/事件回调中调用 `RunAsync` 执行异步逻辑 → 通过 `AsyncState` 暴露状态供界面绑定；具体状态字段与线程切换以源码为准。
- **输入输出规范**：`RunAsync` 为 `protected`，仅派生类可调用；`AsyncState` 为纯状态模型，无外部 I/O。
- **使用约束与注意事项**：`RunAsync` 的异常处理与 UI 线程回调策略由基类约定，派生类不要自行吞异常；`AsyncState` 的线程可见性需遵循调用方约定（待确认）。

## 子目录导航

- [Homepage](./Homepage/agent.md)：首页视图模型。
