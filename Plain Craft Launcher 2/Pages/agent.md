# Pages

界面页面容器，命名空间 `PCL`。聚合启动器各功能页面，并直接提供可复用的左右分栏框架页。

## 本目录直接文件（复用型左右分栏页）

- **版本选择**：`PageSelectLeft.xaml(.cs)`（`PageSelectLeft`，`AddFolder`/`Create_Click`/`Remove_Click`/`Delete_Click`/`Open_Click`/`Refresh_Click`/`RefreshCurrent`/`Rename_Click`/`Folder_Change`，左栏版本列表与文件夹操作）+ `PageSelectRight.xaml(.cs)`（`PageSelectRight`，`McVersionListItem`/`Item_Click`/`DeleteVersion`，右栏版本详情与操作），配套 `PageSelectLeftViewModel`（`IsFirstItem`/`IsLastItem`/`GetDeleteMenuText`/`IsClearingDefaultFolder`）与 `PageSelectRightViewModel`（`MatchesSearch`/`RefreshEmptyDownloadVisibility`）。
- **日志查看**：`PageLogLeft.xaml(.cs)`（`PageLogLeft`，`Add`/`SelectionChange`/`RemoveItem`/`Remove_Click`/`Version_Change`，左栏日志列表）+ `PageLogRight.xaml(.cs)`（`PageLogRight`，`Init`/`Reload`，右栏日志内容），配套 `PageLogLeftViewModel`（`GetItemInfo`/`GetMaxLogCount`）与 `PageLogRightViewModel`（`CanShow`/`RefreshOperationButtons`/`RefreshCounters`/`GetMaxLogHintText`）。
- **网络测速**：`PageSpeedLeft.xaml(.cs)`（`PageSpeedLeft`，`OnNavigatedTo`/`OnNavigatedFrom`/`TaskRefresh`/`TaskRemove`，左栏测速源）+ `PageSpeedRight.xaml(.cs)`（`PageSpeedRight`，`OnNavigatedTo`/`OnNavigatedFrom`，右栏结果），配套 `PageSpeedLeftViewModel`（`Refresh`）。
- **帮助详情**：`PageHelpDetail.xaml(.cs)`（`PageHelpDetail`，`LoadContent`/`Refresh`，含 `HelpContent`）与 `PageHelpDetailViewModel`（`RefreshAsync`/`LoadContent`）。

## 直接代码要点

- **运行流程**：导航进入页面时由 `OnNavigatedTo` 加载数据 → 左栏列表选择触发右栏 `Init`/`Reload` 或 ViewModel `Refresh` → 用户操作（增删改/导出）经事件回调落到模块层 → `OnNavigatedFrom` 释放临时状态。
- **输入输出规范**：输入为导航参数与用户选择（版本、日志、测速项）；输出为 UI 状态与对模块层的调用；ViewModel 暴露绑定属性/命令。
- **使用约束与注意事项**：页面代码运行在 UI 线程；异步加载需防止离开页面后回写（用 `OnNavigatedFrom` 取消/忽略）；左右栏共用同一 ViewModel 数据源时注意刷新时序。

## 子目录导航

- [PageDownload](./PageDownload/agent.md)：下载中心（客户端、Mod 加载器、组件）。
- [PageHomepage](./PageHomepage/agent.md)：主页（新闻/动态）。
- [PageInstance](./PageInstance/agent.md)：游戏实例管理。
- [PageLaunch](./PageLaunch/agent.md)：启动与登录。
- [PageSetup](./PageSetup/agent.md)：设置。
- [PageTools](./PageTools/agent.md)：工具。
