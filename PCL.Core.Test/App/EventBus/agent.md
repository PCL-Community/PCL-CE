# App/EventBus 测试

本目录测试 PCL.Core 中的进程内事件总线服务。

## 本目录测试模块

- `EventBusServiceTest.cs` — `EventBusService` 频道的发布/订阅：基于委托的处理器、基于 `IEventHandler` 实例的处理器、取消订阅后不再触发。验证 `AddChannel`/`Subscribe`/`PublishAsync`/`RemoveChannel` 与通道生命周期（`PCL.Core.App.EventBus`）。
