# App/Tasks 目录

任务调度子系统，位于命名空间 `PCL.Core.App.Tasks`。定义统一的后台任务抽象（可取消、可暂停、可进度、可分组），并提供任务中心进行管理。

## 本目录模块

- **ITask.cs**：任务接口（基本执行单元）。
- **ITaskCancelable.cs**：可取消任务接口。
- **ITaskPausable.cs**：可暂停任务接口。
- **ITaskProgressive.cs**：可进度任务接口。
- **ITaskGroup.cs**：任务组接口（聚合多个任务）。
- **TaskModel.cs**：任务模型（标题、进度、状态等 UI 绑定数据）。
- **TaskState.cs**：任务状态枚举。
- **TaskCenter.cs**：任务中心，负责任务的注册、调度与生命周期管理。
