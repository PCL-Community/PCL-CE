# Utils/Threading 目录

线程基元与线程池，位于命名空间 `PCL.Core.Utils.Threading`。提供异步同步基元与受控并发的执行设施。

## 本目录模块

- **AsyncManualResetEvent.cs**：异步手动重置事件。
- **AsyncCountResetEvent.cs**：异步计数重置事件。
- **AsyncDebounce.cs**：异步防抖（合并高频调用）。
- **LimitedTaskPool.cs**：受限任务池（限制并发数）。
- **LimitedConcurrencyLevelTaskScheduler.cs**：受限并发级别任务调度器。
- **DualThreadPool.cs**：双线程池（区分 IO/计算等不同类别任务）。
