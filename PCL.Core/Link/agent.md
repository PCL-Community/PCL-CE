# Link 目录

联机/P2P 子系统，位于命名空间 `PCL.Core.Link`。负责局域网广播发现、EasyTier 组网、联机大厅、MC 服务器 ping 探测，以及连接脚手架（Scaffolding）的客户端/服务端握手与帧封装。

## 本目录模块

- **`BroadcastListener.cs`** — 局域网广播监听 `BroadcastListener`：`Start` 开始监听、`Stop` 停止、`Dispose` 释放；接收其他实例/服务器广播并产出记录。
- **`BroadcastLocal.cs`** — 局域网广播发送 `BroadcastLocal`：`Start`/`Stop`/`Dispose`，向局域网广播自身/大厅信息。
- **`BroadcastRecord.cs`** — 广播记录模型 `BroadcastRecord`：承载一次广播的来源与内容。
- **`LinkAnnounce.cs`** — 联机公告模型：`LinkAnnounceType`（notice/warning/important 等）、`LinkAnnounceInfo`、`LinkAnnounce`，用于联机状态/公告的展示与传递。

## 直接代码要点

- **运行流程**：`BroadcastLocal.Start` 周期发送广播；`BroadcastListener.Start` 加入监听并在收到广播时生成 `BroadcastRecord`；联机公告经 `LinkAnnounce` 模型在 UI 呈现；`Stop`/`Dispose` 释放套接字与后台任务。
- **输入输出规范**：广播为 UDP 数据报，序列化为 `BroadcastRecord`；公告为结构化模型（类型 + 信息）。
- **使用约束与注意事项**：广播端口可能被占用或被防火墙拦截；`Start`/`Stop` 需成对调用，`Dispose` 后不可复用；局域网发现依赖同一网段与组播/广播配置。

## 子目录导航

- [EasyTier](./EasyTier/agent.md)：EasyTier 组网控制与信息。
- [Lobby](./Lobby/agent.md)：联机大厅控制与服务。
- [McPing](./McPing/agent.md)：Minecraft 服务器 ping 探测。
- [Natayark](./Natayark/agent.md)：Natayark 联机档案管理。
- [Scaffolding](./Scaffolding/agent.md)：连接脚手架（握手/帧封装）。
