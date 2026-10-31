# Modules/Event

可在 XAML 中以声明式方式绑定、触发动作（如打开网址、启动游戏、刷新页面）的 **自定义事件系统**，命名空间 `PCL`。

## 本目录直接文件

- `CustomEvent.cs`：单条自定义事件，由 `Type`（`EventType`）+ `Data`（数据字符串）组成。
- `CustomEventCollection.cs`：事件集合，承载某个控件/场景下的多条事件。
- `CustomEventService.cs`：通过附加属性 `Events`（`DependencyProperty`）将事件集合注册到任意 `DependencyObject`，并提供 `SetEvents`/`GetEvents`。
- `EventType.cs`：事件类型枚举（`OpenUrl`/`OpenFile`/`ExecuteCommand`/`LaunchGame`/`CopyText`/`RefreshHomepage`/`RefreshPage`/`SwitchPage`/`ImportModpack` 等）。
- `EventTypeMapper.cs`：事件类型与处理逻辑的映射。
- `XamlEventSanitizer.cs`：XAML 事件的安全解析/消毒，防止注入或非法事件定义。
