# Controls/Behaviors

WPF 交互行为（Behaviors）实现，命名空间 `PCL`。

## 本目录直接文件

- `ClipboardInterceptor.cs`：剪贴板拦截处理（源自 MaaAssistantArknights 项目，Apache-2.0，仅本文件），用于在控件内安全拦截/处理剪贴板操作。
- `LazyLoadBehavior.cs`：提供 `LazyLoader.EnableLazyLoad` 扩展方法，实现控件的懒加载行为（延迟初始化直到实际需要渲染）。
