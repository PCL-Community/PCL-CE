# App/EventBus 目录

事件总线子系统，位于命名空间 `PCL.Core.App.EventBus`。提供基于频道（channel）的发布/订阅机制，支持按事件数据类型分发的弱引用处理器。

## 本目录模块

- **EventBusService.cs**：事件总线服务（`[LifecycleService]`），管理频道与处理器注册、事件发布。
- **EventDataBase.cs**：事件数据基类，所有事件载荷的基类。
- **IEventHandler.cs**：事件处理器接口（接收 `EventDataBase`）。
- **IResponsibleEventHandler.cs**：可响应型事件处理器接口（处理后可返回响应）。
