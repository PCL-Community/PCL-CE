# Pages/PageInstance

游戏实例（存档/配置隔离的 Minecraft 环境）管理主页面容器，命名空间 `PCL`。

## 本目录直接文件

- **`PageInstance.xaml(.cs)`** — 实例管理容器/导航页。
- **`PageInstanceLeft.xaml(.cs)`** — 左侧实例列表 `PageInstanceLeft`：`Refresh`/`RefreshModDisabled`/`Reset`/`PageGet`/`PageChange`，配套 `PageInstanceLeftViewModel`（`RefreshModDisabled`、各类提示文案）。
- **`PageInstanceOverall.xaml(.cs)`** — 实例总览 `PageInstanceOverall`：`OpenVersionFolder`，配套 `PageInstanceOverallViewModel`（`BuildInfoRows`/`RefreshDisplay`）。
- **`PageInstanceSetup.xaml(.cs)`** — 实例设置 `PageInstanceSetup`：`Reload`/`Reset`/`RamType`/`RefreshRam`/`GetRam`/`GetInitialRam`/`ServerLogin`/`RefreshJavaComboBox`，配套 `PageInstanceSetupViewModel`。
- **`PageInstanceInstall.xaml(.cs)`** — 实例内安装 `PageInstanceInstall`：`ExitSelectPage`/`MinecraftSelected`/`GetCurrentInfo`/`Fabric_Selected`/`LegacyFabric_Selected`/`LabyMod_Selected`，配套 `PageInstanceInstallViewModel`（`GetVersionTypeTitle`/`GetSelectLogo`/`GetSelectInfo`）。
- **`PageInstanceExport.xaml(.cs)`** — 实例导出 `PageInstanceExport`：`RefreshAll`，配套 `PageInstanceExportViewModel`（`RefreshHeader`/`RefreshOtherFolders`）。
- **`PageInstanceServer.xaml(.cs)` / `ServerCard.xaml(.cs)`** — 实例联机服务器 `PageInstanceServer`（`RefreshServers`/`PingServerAsync`，含 `MinecraftServerInfo`/`ServerStatus`）与服务器卡片 `ServerCard`（`UpdateServerInfo`/`RefreshServerStatusAsync`）。
- **`PageInstanceScreenshot.xaml(.cs)`** — 实例截图浏览 `PageInstanceScreenshot`：`RefreshAsync`/`ReloadAsync`，配套 `PageInstanceScreenshotViewModel`。
- **`PageInstanceModDisabled.xaml(.cs)`** — 已禁用模组列表 `PageInstanceModDisabled`，配套 `PageInstanceModDisabledViewModel`。
- **`PageInstanceCompResource.xaml(.cs)`** — 实例资源组件管理 `PageInstanceCompResource`：`ReloadCompFileList`/`Refresh`/`LoaderRun`/`InstallMods`/`InstallCompFiles`/`CheckChanged`/`UpdateResource` 等，配套 `PageInstanceCompResourceViewModel`（`ShowListPanel`/`ShowEmptyPanel`）。
- **`PageInstanceCompJarInJar.xaml(.cs)`** — 实例 `JarInJar` 组件管理 `PageInstanceCompJarInJar`：`OnJijResolved`，配套 `PageInstanceCompJarInJarViewModel`（`HideLoading`/`ApplySectionVisibility`）。
- **`PageInstanceSaves.xaml(.cs)`** — 实例存档页 `PageInstanceSaves`：`Refresh`/`Reload`，配套 `PageInstanceSavesViewModel`（`LoadListAsync`/`SearchAsync`/`DeleteSaveAsync` 等；子目录 `PageInstanceSaves` 另含细分实现）。
- **`MyLocalModItem.xaml(.cs)`** — 本地模组项控件 `MyLocalCompItem`：`Refresh`/`RefreshColor`。

## 直接代码要点

- **运行流程**：`PageInstanceLeft` 选择实例 → 右侧各子页 `OnNavigatedTo` 加载对应数据（总览/设置/组件/存档/截图/服务器）→ 用户操作调用模块层（`ModInstanceList`/`ModComp`/存档管理等）→ `OnNavigatedFrom` 保存/释放。
- **输入输出规范**：输入为当前实例与子页参数；输出为 UI 状态、模块层调用与导航事件；ViewModel 提供绑定属性/命令。
- **使用约束与注意事项**：实例切换需重置子页状态（`Reset`）；异步加载在离开页面后不得回写；组件/存档操作需处理占用与并发；删除前需确认。

## 子目录导航

- [PageInstanceSaves](./PageInstanceSaves/agent.md)：实例存档浏览器。
