# Modules/UI

UI 支撑模块，命名空间 `PCL`。提供轻提示（Hint）队列、提示类型定义、页面视图抽象与导航服务，并桥接主题管理。

## 本目录直接文件

- **`HintService.cs`** — 轻提示服务 `HintService`（含 `HintMessage`）：维护待展示提示队列（`HintWaiting`），`Hint()` 入队、`HintWrapper_OnShow` 事件与界面交互、`Tick` 定时推进、`HideAll` 清空，按类型展示并可记录日志。
- **`HintType.cs`** — 提示类型枚举 `HintType`（信息/成功/警告/错误等）。
- **`IPageView.cs`** — 页面视图接口 `IPageView`：约束页面在导航生命周期中的统一行为。
- **`NavigationService.cs`** — 导航服务 `NavigationService`（`INavigationService`）：`GoTo`/`GoBack` 页面跳转，`RouteKey` 标识路由，`RaiseNavigated` 广播导航事件。

## 直接代码要点

- **运行流程**：业务调用 `HintService.Hint` 入队 → `Tick`/`HintWrapper_OnShow` 按序弹出并在超时后移除；页面跳转经 `NavigationService.GoTo`/`GoBack`，并触发 `IPageView` 的导航回调。
- **输入输出规范**：提示输入为文本 + `HintType`（可附日志）；导航输入为 `RouteKey`/页面参数，输出为导航事件。
- **使用约束与注意事项**：提示队列与 UI 更新须在 UI 线程；`HideAll` 用于切换实例/退出时清理；导航需避免重复入栈与循环跳转；`IPageView` 实现应正确处理 `OnNavigatedTo/From`。

## 子目录导航

- [Theme](./Theme/agent.md)：主题与暗色/亮色模式管理（ThemeManager）。
