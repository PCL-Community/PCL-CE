# Pages/PageDownload

下载中心主页面容器，命名空间 `PCL`。提供 Minecraft 客户端与各 Mod 加载器/组件的下载入口。

## 本目录直接文件

- **`ModDownloadLib.cs`** — 下载中心共享逻辑库，供各下载子页面复用（版本列表、下载/安装流程的公共步骤）。
- **`PageDownload.xaml(.cs)`** — 下载中心容器/导航页。
- **`PageDownloadLeft.xaml(.cs)`** — 左侧导航栏 `PageDownloadLeft`：`Refresh`、`RefreshButton_Click`、`PageGet`、`PageChange`。
- **`PageDownloadClient.xaml(.cs)`** — 游戏客户端下载 `PageDownloadClient`。
- **加载器下载**：`PageDownloadForge`（`Forge_Click`/`Forge_StateChanged`）、`PageDownloadFabric`、`PageDownloadNeoForge`、`PageDownloadOptiFine`、`PageDownloadLiteLoader`（`DownloadStart`）、`PageDownloadLabyMod`、`PageDownloadLegacyFabric`（各加载器下载与安装引导）。
- **`PageDownloadInstall.xaml(.cs)`** — 安装引导 `PageDownloadInstall`：`ExitSelectPage`、`MinecraftSelected`、`Fabric_Selected`/`IsFabricApiCompatible`、`LegacyFabric_Selected`/`IsSuitableLegacyFabricApi`、`LabyMod_Selected`，并配套 `PageDownloadInstallViewModel`。
- **`PageDownloadCleanroom.xaml(.cs)`** — Cleanroom 加载器下载 `PageDownloadCleanroom`。
- **`PageDownloadCompFavorites.xaml(.cs)`** — 组件收藏 `PageDownloadCompFavorites`：`SearchRun`、`RefreshLocalization`，含 `CompListItemContainer`；配套 `PageDownloadCompFavoritesViewModel`（`GetCategoryTitleFormat`）。

## 直接代码要点

- **运行流程**：`PageDownloadLeft` 切换子页 → 选择游戏版本 → 各加载器页加载可选版本并触发安装 → `PageDownloadInstall` 汇总选择并执行安装（经 `ModDownloadLib`）→ 完成后导航离开。
- **输入输出规范**：输入为游戏版本、加载器类型与版本、安装选项；输出为下载/安装任务与进度 UI。
- **使用约束与注意事项**：安装前需校验加载器与游戏版本兼容（如 Fabric API 兼容性）；下载任务需支持取消与失败提示；共享逻辑集中在 `ModDownloadLib`，避免各页重复实现。

## 子目录导航

- [Comp](./Comp/agent.md)：组件（模组/资源包/光影/数据包/地图/整合包）下载与管理。
