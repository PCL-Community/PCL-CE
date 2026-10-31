# Modules/UI/Theme

主题管理，命名空间 `PCL`。桥接 `PCL.Core.UI.Theme.ThemeService`，统一刷新启动器界面外观。

## 本目录直接文件

- `ThemeManager.cs`：静态主题管理器。提供 `IsDarkMode`（`ThemeService.IsDarkMode`）、`AppResources`（应用资源字典），在颜色模式/主题变更时调用 `ThemeRefresh()` 刷新全局资源；注册右键菜单等上下文处理。
