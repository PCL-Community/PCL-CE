# Modules

PCL2 的 **业务功能模块容器**，命名空间 `PCL`。聚合启动器各领域的核心逻辑，由子目录按主题组织。

## 本目录直接文件

- `ModMain.cs`：核心协调器。持有各页面实例引用（`frmMain`、`frmLaunchLeft/Right`、`frmLogLeft/Right`、`frmSelectLeft/Right`、`frmSpeedLeft/Right`、`frmTools*`、`frmDownloadLeft` 等）；封装 `MyMsgBox` / `MyMsgBoxMarkdown` 消息框、`SetGPUPreference` 显卡偏好、反馈信息收集等全局能力。
- `ModLink.cs`：联机功能（大厅操作、局域网穿透、EasyTier/Natayark 等），依赖 `PCL.Core.Link.*`。
- `ModMusic.cs`：基于 NAudio 的背景音乐播放（`WaveOutEvent` 循环播放）。
- `ModVideoBack.cs`：视频背景播放控制，维护 `IsGaming` / `ForcePlay` 状态及对应事件（判断用户是否在游戏中以决定是否暂停背景视频）。
- `ModWebServer.cs`：内嵌 HTTP 服务端管理（`StartWebServer` / 停止），用于本地服务响应。

## 子目录导航

- [Base](./Base/agent.md)：最底层基础工具与通用模块。
- [Event](./Event/agent.md)：可声明式触发的自定义事件系统。
- [Minecraft](./Minecraft/agent.md)：Minecraft 实例、版本、组件、启动与崩溃分析。
- [Network](./Network/agent.md)：网络请求、下载与连接管理。
- [UI](./UI/agent.md)：提示（Hint）与主题管理。
- [Updates](./Updates/agent.md)：更新检查与公告。
