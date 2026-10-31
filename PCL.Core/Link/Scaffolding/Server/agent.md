# Link/Scaffolding/Server 目录

脚手架服务端，位于命名空间 `PCL.Core.Link.Scaffolding.Server`。接收客户端连接、按请求类型分派到处理器，并跟踪已连接玩家。

## 本目录模块

- **`ScaffoldingServer.cs`** — 脚手架服务端 `ScaffoldingServer`：`Start` 开始监听并接受连接，`DisposeAsync` 停止服务并释放资源；按请求类型分派到 `Handlers`。
- **`ScaffoldingServerContext.cs`** — 服务端上下文 `ScaffoldingServerContext`：`Create` 构建上下文，`OnPlayerProfilesChanged` 在玩家档案变化时通知（供处理器读取连接/状态）。
- **`TrackedPlayerProfile.cs`** — 被跟踪玩家档案 `TrackedPlayerProfile`：记录在线状态与连接关联，作为服务端玩家表元素。

## 直接代码要点

- **运行流程**：`Start` 绑定端口并进入接受循环 → 每个连接读取帧、按 `RequestType` 查表分派到对应 `IRequestHandler` → 处理器经 `IServerContext` 读写玩家表并回写响应 → 玩家变化触发 `OnPlayerProfilesChanged`。
- **输入输出规范**：输入为客户端帧（类型 + body），输出为响应帧；玩家档案为 `TrackedPlayerProfile` 集合。
- **使用约束与注意事项**：处理器需线程安全（多连接并发）；玩家表更新要原子化；`DisposeAsync` 需等待在途连接结束；会话标识仅用于日志，不作为鉴权。

## 子目录导航

- [Abstractions](./Abstractions/agent.md)：请求处理器与上下文接口。
- [Handlers](./Handlers/agent.md)：具体请求处理器。
